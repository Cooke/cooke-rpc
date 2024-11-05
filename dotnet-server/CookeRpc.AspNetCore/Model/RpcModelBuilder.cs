using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelBuilder
    {
        private readonly RpcModelBuilderOptions _options;
        private readonly Dictionary<Type, IRpcType> _mappings = new();
        private readonly Dictionary<Type, RpcServiceModel> _services = new();

        public RpcModelBuilder()
            : this(new RpcModelBuilderOptions()) { }

        public RpcModelBuilder(RpcModelBuilderOptions options)
        {
            _options = options;
        }

        public IRpcType MapType(Type clrType, IRpcType type)
        {
            _mappings.Add(clrType, type);
            return type;
        }

        public IRpcType MapType(Type clrType)
        {
            return MapType(clrType, (NullabilityInfo?)null);
        }

        public IRpcType MapType(Type clrType, NullabilityInfo? nullabilityInfo)
        {
            if (nullabilityInfo?.ReadState == NullabilityState.Nullable)
            {
                return new UnionRpcType([Map(), PrimitiveTypes.Null], clrType);
            }

            return Map();

            IRpcType Map()
            {
                var knownType = _mappings.GetValueOrDefault(clrType);
                if (knownType != null)
                {
                    return knownType;
                }

                if (_options.OnMappingType != null)
                {
                    _options.OnMappingType.Invoke(clrType, this);

                    var knownType2 = _mappings.GetValueOrDefault(clrType);
                    if (knownType2 != null)
                    {
                        return knownType2;
                    }
                }

                if (!_options.TypeFilter(clrType))
                {
                    throw new InvalidOperationException(
                        $"Type ${clrType} cannot be mapped due to the type filter configuration"
                    );
                }

                if (_options.MapResolutions.TryGetValue(clrType, out var resolution))
                {
                    _mappings.Add(clrType, resolution);
                    return resolution;
                }

                var rpcTypeAttribute = clrType.GetCustomAttribute<RpcTypeAttribute>();
                if (rpcTypeAttribute?.Kind == RpcTypeKind.Union)
                {
                    return MapUnionType(clrType);
                }

                if (rpcTypeAttribute?.Kind == RpcTypeKind.Primitive)
                {
                    var type = new PrimitiveRpcType(
                        rpcTypeAttribute.Name ?? _options.TypeNameFormatter(clrType),
                        clrType
                    );
                    _mappings.Add(clrType, type);
                    return type;
                }

                var genericDictionary =
                    ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IDictionary<,>))
                    ?? ReflectionHelper.GetGenericTypeOfDefinition(
                        clrType,
                        typeof(IReadOnlyDictionary<,>)
                    );
                if (genericDictionary != null)
                {
                    return MapMapType(
                        clrType,
                        genericDictionary,
                        nullabilityInfo?.GenericTypeArguments[1]
                    );
                }

                if (clrType.IsArray)
                {
                    return MapArrayType(
                        clrType,
                        clrType.GetElementType()
                            ?? throw new InvalidOperationException(
                                "Array type must have an element type"
                            ),
                        nullabilityInfo?.ElementType
                    );
                }

                var genericEnumerable = ReflectionHelper.GetGenericTypeOfDefinition(
                    clrType,
                    typeof(IEnumerable<>)
                );
                if (genericEnumerable != null)
                {
                    return MapArrayType(
                        clrType,
                        genericEnumerable.GetGenericArguments()[0],
                        nullabilityInfo?.GenericTypeArguments.FirstOrDefault()
                    );
                }

                var asyncEnumerable = ReflectionHelper.GetGenericTypeOfDefinition(
                    clrType,
                    typeof(IAsyncEnumerable<>)
                );
                if (asyncEnumerable != null)
                {
                    return MapArrayType(
                        clrType,
                        asyncEnumerable.GetGenericArguments()[0],
                        nullabilityInfo?.GenericTypeArguments.FirstOrDefault()
                    );
                }

                var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(
                    clrType,
                    typeof(Nullable<>)
                );
                if (genericNullable != null)
                {
                    return MapNullableType(clrType, genericNullable);
                }

                var optional = ReflectionHelper.GetGenericTypeOfDefinition(
                    clrType,
                    typeof(Optional<>)
                );
                if (optional != null)
                {
                    return MapOptionalType(clrType, optional);
                }

                if (clrType.IsAssignableTo(typeof(ITuple)))
                {
                    return MapTupleType(clrType);
                }

                if (clrType.IsGenericType && !clrType.IsGenericTypeDefinition)
                {
                    var definitionType = MapType(clrType.GetGenericTypeDefinition());
                    if (definitionType is not INamedRpcType namedRpcType)
                    {
                        throw new NotSupportedException(
                            "Generic type definitions must map to a named rpc type"
                        );
                    }

                    var typeArguments = clrType.GenericTypeArguments.Select(MapType).ToList();
                    return new GenericRpcType(clrType, namedRpcType, typeArguments);
                }

                if (clrType.IsGenericTypeParameter)
                {
                    var typeParameter = new TypeParameterRpcType(
                        _options.TypeNameFormatter(clrType),
                        clrType
                    );
                    _mappings.Add(clrType, typeParameter);
                    return typeParameter;
                }

                if (clrType.IsClass || clrType.IsInterface)
                {
                    return MapObjectType(clrType);
                }

                if (clrType.IsEnum)
                {
                    return MapEnumType(clrType);
                }

                throw new ArgumentException($"Invalid type {clrType}");
            }
        }

        private IRpcType MapTupleType(Type clrType)
        {
            var typeArguments = new List<IRpcType>();
            var tupleType = (PrimitiveRpcType)MapType(typeof(ITuple));
            var genericType = new GenericRpcType(clrType, tupleType, typeArguments);
            _mappings.Add(clrType, genericType);
            typeArguments.AddRange(clrType.GenericTypeArguments.Select(MapType));
            return genericType;
        }

        private IRpcType MapOptionalType(Type clrType, Type optional)
        {
            var typeArguments = new List<IRpcType>();
            var optionalType = (PrimitiveRpcType)MapType(typeof(Optional<>));
            var genericType = new GenericRpcType(clrType, optionalType, typeArguments);
            _mappings.Add(clrType, genericType);
            typeArguments.Add(MapType(optional.GetGenericArguments().Single()));
            return genericType;
        }

        private IRpcType MapNullableType(Type clrType, Type genericNullable)
        {
            var typeArguments = new List<IRpcType> { PrimitiveTypes.Null };
            var unionType = new UnionRpcType(typeArguments, clrType);
            _mappings.Add(clrType, unionType);
            typeArguments.Add(MapType(genericNullable.GetGenericArguments().Single()));
            return unionType;
        }

        private IRpcType MapArrayType(
            Type clrType,
            Type elementType,
            NullabilityInfo? elementNullabilityInfo
        )
        {
            var typeArguments = new List<IRpcType>();
            var arrayType = (PrimitiveRpcType)MapType(
                typeof(IEnumerable<>),
                (NullabilityInfo?)null
            );
            var genericType = new GenericRpcType(clrType, arrayType, typeArguments);
            _mappings.Add(clrType, genericType);
            typeArguments.Add(MapType(elementType, elementNullabilityInfo));
            return genericType;
        }

        private IRpcType MapMapType(
            Type clrType,
            Type genericDictionary,
            NullabilityInfo? valueNullabilityInfo
        )
        {
            var keyType = genericDictionary.GenericTypeArguments[0];
            var valueType = genericDictionary.GenericTypeArguments[1];
            var typeArguments = new List<IRpcType>();
            var mapType = (PrimitiveRpcType)MapType(typeof(Dictionary<,>));
            var genericType = new GenericRpcType(clrType, mapType, typeArguments);
            _mappings.Add(clrType, genericType);
            typeArguments.AddRange(
                new[] { MapType(keyType), MapType(valueType, valueNullabilityInfo) }
            );
            return genericType;
        }

        private IRpcType MapObjectType(Type clrType)
        {
            string typeName = _options.TypeNameFormatter(clrType);
            var props = new List<RpcPropertyDefinition>();
            var extends = new List<IRpcType>(); // Can only be reffed object type or generic type
            var typeParameters = new List<TypeParameterRpcType>();
            var type = new ObjectRpcType(
                clrType,
                props,
                extends,
                clrType.IsAbstract || clrType.IsInterface,
                typeName,
                typeParameters
            );
            _mappings.Add(clrType, type);
            var rpcType = type;

            if (
                clrType.BaseType != typeof(object)
                && clrType.BaseType != null
                && _options.TypeFilter(clrType.BaseType)
            )
            {
                var rpcBaseType = MapType(clrType.BaseType);
                extends.Add(rpcBaseType);
            }

            foreach (var @interface in clrType.GetInterfaces().Where(_options.InterfaceFilter))
            {
                var interfaceRpcType = MapType(@interface);
                extends.Add(interfaceRpcType);
            }

            props.AddRange(CreatePropertyDefinitions(clrType).OrderBy(x => x.Name));

            foreach (
                var subType in ReflectionHelper
                    .FindAllOfType(clrType)
                    .Except(new[] { clrType })
                    .Where(_options.TypeFilter)
            )
            {
                MapType(subType);
            }

            if (clrType.IsTypeDefinition)
            {
                typeParameters.AddRange(
                    clrType.GetGenericArguments().Select(MapType).Cast<TypeParameterRpcType>()
                );
                foreach (
                    var subType in ReflectionHelper
                        .GetAllUserTypes()
                        .Where(x =>
                            x.BaseType?.IsGenericType == true
                            && x.BaseType.GetGenericTypeDefinition() == clrType
                        )
                        .Where(_options.TypeFilter)
                )
                {
                    MapType(subType);
                }
            }

            return rpcType;
        }

        private IEnumerable<RpcPropertyDefinition> CreatePropertyDefinitions(Type clrType)
        {
            var memberInfos = clrType
                .GetMembers(_options.MemberBindingFilter)
                .Where(_options.MemberFilter);

            var props = new List<RpcPropertyDefinition>();

            foreach (var memberInfo in memberInfos)
            {
                switch (memberInfo)
                {
                    case FieldInfo fieldInfo when _options.TypeFilter(fieldInfo.FieldType):
                    {
                        props.Add(
                            CreatePropertyDefinition(
                                memberInfo,
                                fieldInfo.FieldType,
                                GetNullabilityInfo(fieldInfo)
                            )
                        );
                        break;
                    }

                    case PropertyInfo propertyInfo
                        when _options.TypeFilter(propertyInfo.PropertyType):
                    {
                        props.Add(
                            CreatePropertyDefinition(
                                memberInfo,
                                propertyInfo.PropertyType,
                                GetNullabilityInfo(propertyInfo)
                            )
                        );

                        break;
                    }
                }
            }

            return props;

            RpcPropertyDefinition CreatePropertyDefinition(
                MemberInfo memberInfo,
                Type type,
                NullabilityInfo nullabilityInfo
            ) =>
                new(
                    _options.MemberNameFormatter(memberInfo),
                    MapType(type, nullabilityInfo),
                    memberInfo
                )
                {
                    IsOptional = nullabilityInfo.WriteState == NullabilityState.Nullable,
                };
        }

        private IRpcType MapUnionType(Type clrType)
        {
            var memberTypes = new List<IRpcType>();
            var type = new NamedUnionRpcType(
                _options.TypeNameFormatter(clrType),
                memberTypes,
                clrType
            );
            _mappings.Add(clrType, type);
            memberTypes.AddRange(
                ReflectionHelper
                    .FindAllOfType(clrType)
                    .Except(new[] { clrType })
                    .Where(_options.TypeFilter)
                    .Select(MapType)
            );
            return type;
        }

        private IRpcType MapEnumType(Type clrType)
        {
            var enumType = new EnumRpcType(
                _options.TypeNameFormatter(clrType),
                clrType,
                Enum.GetNames(clrType)
                    .Zip(
                        Enum.GetValues(clrType).Cast<int>(),
                        (name, val) =>
                            new RpcEnumMember(_options.EnumMemberNameFormatter(name), val)
                    )
                    .ToList()
            );
            _mappings.Add(clrType, enumType);
            return enumType;
        }

        public RpcServiceModel AddService(Type serviceType)
        {
            if (_services.ContainsKey(serviceType))
            {
                return _services[serviceType];
            }

            var serviceName = _options.ServiceNameFormatter(serviceType);

            var procedures = new List<RpcProcedureModel>();
            var serviceModel = new RpcServiceModel(
                serviceType,
                serviceName,
                procedures,
                serviceType.GetCustomAttributes().ToArray()
            );

            var methods = serviceType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.DeclaringType != typeof(object));

            foreach (var method in methods)
            {
                var (rpcDelegate, parameterInfos, returnType) = RpcDelegateFactory.Create(
                    method,
                    _options.ContextType,
                    _options.CustomParameterResolver
                );

                List<RpcParameterModel> rpcParameterModels = new();
                foreach (var parameterInfo in parameterInfos)
                {
                    var innerType = MapType(
                        parameterInfo.ParameterType,
                        GetNullabilityInfo(parameterInfo)
                    );
                    var paraModel = new RpcParameterModel(
                        parameterInfo.Name ?? throw new InvalidOperationException(),
                        innerType,
                        parameterInfo.HasDefaultValue
                    );

                    rpcParameterModels.Add(paraModel);
                }

                var taskType = ReflectionHelper.GetGenericTypeOfDefinition(
                    method.ReturnType,
                    typeof(Task<>)
                );
                var rpcReturnType =
                    taskType != null
                        ? MapType(
                            taskType.GenericTypeArguments[0],
                            GetNullabilityInfo(method.ReturnParameter).GenericTypeArguments[0]
                        )
                        : MapType(returnType, GetNullabilityInfo(method.ReturnParameter));

                var procModel = new RpcProcedureModel(
                    _options.ProcedureNameFormatter(method),
                    rpcDelegate,
                    rpcReturnType,
                    rpcParameterModels,
                    method.GetCustomAttributes().ToArray()
                );

                procedures.Add(procModel);
            }

            _services.Add(serviceType, serviceModel);
            return serviceModel;
        }

        private static NullabilityInfo GetNullabilityInfo(ParameterInfo parameter) =>
            new NullabilityInfoContext().Create(parameter);

        private static NullabilityInfo GetNullabilityInfo(FieldInfo fieldInfo) =>
            new NullabilityInfoContext().Create(fieldInfo);

        private static NullabilityInfo GetNullabilityInfo(PropertyInfo propertyInfo) =>
            new NullabilityInfoContext().Create(propertyInfo);

        public RpcModel Build()
        {
            return new RpcModel(
                _mappings
                    .Values.Where(x => x is not TypeParameterRpcType)
                    .OfType<INamedRpcType>()
                    .Distinct()
                    .OrderBy(x =>
                        x switch
                        {
                            PrimitiveRpcType => 0,
                            EnumRpcType => 1,
                            NamedUnionRpcType => 2,
                            ObjectRpcType => 3,
                            _ => throw new ArgumentOutOfRangeException(nameof(x))
                        }
                    )
                    .ThenBy(x => x.Name)
                    .ToList(),
                _services.Values
            );
        }
    }
}
