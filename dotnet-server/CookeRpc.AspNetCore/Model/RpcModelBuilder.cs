using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModelBuilder
    {
        private readonly RpcModelBuilderOptions _options;
        private readonly List<INamedRpcType> _types;
        private readonly Dictionary<Type, IRpcType> _mappings;
        private readonly Dictionary<Type, RpcServiceModel> _services = new();

        public RpcModelBuilder() : this(new RpcModelBuilderOptions())
        {
        }

        public RpcModelBuilder(RpcModelBuilderOptions options)
        {
            _options = options;
            _types = new();
            _mappings = new();
        }

        public IRpcType MapType(Type clrType)
        {
            var knownType = _mappings.GetValueOrDefault(clrType);
            if (knownType != null) {
                return knownType;
            }

            if (_options.OnAddingType != null) {
                _options.OnAddingType.Invoke(clrType, this);

                var knownType2 = _mappings.GetValueOrDefault(clrType);
                if (knownType2 != null) {
                    return knownType2;
                }
            }

            if (_options.PrimitiveTypeMap.TryGetValue(clrType, out var primitiveType)) {
                if (!_types.Contains(primitiveType)) {
                    _types.Add(primitiveType);
                }

                _mappings.Add(clrType, primitiveType);
                return primitiveType;
            }

            if (!_options.TypeFilter(clrType)) {
                throw new InvalidOperationException(
                    $"Type ${clrType} cannot be mapped due to the type filter configuration");
            }

            var rpcTypeAttribute = clrType.GetCustomAttribute<RpcTypeAttribute>();
            if (rpcTypeAttribute?.Kind == RpcTypeKind.Union) {
                return MapUnionType(clrType);
            }

            if (rpcTypeAttribute?.Kind == RpcTypeKind.Primitive) {
                var type = new PrimitiveRpcType(rpcTypeAttribute.Name ?? _options.TypeNameFormatter(clrType), clrType);
                _mappings.Add(clrType, type);

                if (!_types.Contains(type)) {
                    _types.Add(type);
                }
                return type;
            }

            var genericDictionary = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IDictionary<,>)) ??
                                    ReflectionHelper.GetGenericTypeOfDefinition(clrType,
                                        typeof(IReadOnlyDictionary<,>));
            if (genericDictionary != null) {
                var keyType = genericDictionary.GenericTypeArguments[0];
                var valueType = genericDictionary.GenericTypeArguments[1];
                var typeArguments = new List<IRpcType>();
                var mapType = (PrimitiveRpcType)MapType(typeof(Dictionary<,>));
                var genericType = new GenericRpcType(clrType, mapType, typeArguments);
                _mappings.Add(clrType, genericType);
                typeArguments.AddRange(new[]
                {
                    MapType(keyType), MapType(valueType)
                });
                return genericType;
            }

            var genericClrArray = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IEnumerable<>));
            if (genericClrArray != null) {
                var typeArguments = new List<IRpcType>();
                var arrayType = (PrimitiveRpcType)MapType(typeof(IEnumerable<>));
                var genericType = new GenericRpcType(clrType, arrayType, typeArguments);
                _mappings.Add(clrType, genericType);
                typeArguments.Add(MapType(genericClrArray.GenericTypeArguments[0]));
                return genericType;
            }

            var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Nullable<>));
            if (genericNullable != null) {
                var typeArguments = new List<IRpcType>
                {
                    PrimitiveTypes.Null
                };
                var unionType = new UnionRpcType(typeArguments, clrType);
                _mappings.Add(clrType, unionType);
                typeArguments.Add(MapType(genericNullable.GetGenericArguments().Single()));
                return unionType;
            }

            var optional = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Optional<>));
            if (optional != null) {
                var typeArguments = new List<IRpcType>();
                var optionalType = (PrimitiveRpcType)MapType(typeof(Optional<>));
                var genericType = new GenericRpcType(clrType, optionalType, typeArguments);
                _mappings.Add(clrType, genericType);
                typeArguments.Add(MapType(optional.GetGenericArguments().Single()));
                return genericType;
            }

            if (clrType.IsAssignableTo(typeof(ITuple))) {
                var typeArguments = new List<IRpcType>();
                var tupleType = (PrimitiveRpcType)MapType(typeof(ITuple));
                var genericType = new GenericRpcType(clrType, tupleType, typeArguments);
                _mappings.Add(clrType, genericType);
                typeArguments.AddRange(clrType.GenericTypeArguments.Select(MapType));
                return genericType;
            }

            if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() != clrType) {
                return MapType(clrType.GetGenericTypeDefinition());
            }

            if (clrType.IsClass || clrType.IsInterface) {
                return MapObject(clrType);
            }

            if (clrType.IsEnum) {
                return MapEnum(clrType);
            }

            throw new ArgumentException($"Invalid type {clrType}");
        }

        public RpcServiceModel AddService(Type serviceType)
        {
            if (_services.ContainsKey(serviceType)) {
                return _services[serviceType];
            }

            var serviceName = _options.ServiceNameFormatter(serviceType);

            var procedures = new List<RpcProcedureModel>();
            var serviceModel = new RpcServiceModel(serviceType, serviceName, procedures,
                serviceType.GetCustomAttributes().ToArray());

            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.DeclaringType != typeof(object));

            foreach (var method in methods) {
                var (rpcDelegate, parameterInfos, returnType) = RpcDelegateFactory.Create(method, _options.ContextType,
                    _options.CustomParameterResolver);

                List<RpcParameterModel> rpcParameterModels = new();
                foreach (var parameterInfo in parameterInfos) {
                    var rpcAttr = parameterInfo.GetCustomAttribute<RpcTypeAttribute>();
                    var paraType = rpcAttr is {Name: { }} ? new PrimitiveRpcType(rpcAttr.Name, parameterInfo.ParameterType) :
                        ReflectionHelper.IsNullable(parameterInfo) ? MakeNullable(MapType(parameterInfo.ParameterType)) : MapType(parameterInfo.ParameterType);

                    var paraModel = new RpcParameterModel(parameterInfo.Name ?? throw new InvalidOperationException(),
                        paraType, parameterInfo.HasDefaultValue);

                    rpcParameterModels.Add(paraModel);
                }

                var rpcReturnType = MapType(returnType);
                var returnTypeModel = ReflectionHelper.IsNullableReturn(method)
                    ? new UnionRpcType(new[]
                    {
                        PrimitiveTypes.Null, rpcReturnType
                    }, method.ReturnType)
                    : rpcReturnType;

                var procModel = new RpcProcedureModel(_options.ProcedureNameFormatter(method), rpcDelegate,
                    returnTypeModel, rpcParameterModels, method.GetCustomAttributes().ToArray());

                procedures.Add(procModel);
            }

            _services.Add(serviceType, serviceModel);
            return serviceModel;
        }

        private IRpcType MapObject(Type clrType)
        {
            string typeName = _options.TypeNameFormatter(clrType);
            var props = new List<RpcPropertyDefinition>();
            var extends = new List<IRpcType>(); // Can only be reffed object type or generic type
            var type = new ObjectRpcType(clrType, props, extends, clrType.IsAbstract || clrType.IsInterface, typeName);
            _types.Add(type);
            _mappings.Add(clrType, type);
            var rpcType = type;

            if (clrType.BaseType != typeof(object) && clrType.BaseType != null &&
                _options.TypeFilter(clrType.BaseType)) {
                var rpcBaseType = MapType(clrType.BaseType);
                extends.Add(rpcBaseType);
            }

            foreach (var @interface in clrType.GetInterfaces().Where(_options.InterfaceFilter)) {
                var interfaceRpcType = MapType(@interface);
                extends.Add(interfaceRpcType);
            }

            props.AddRange(CreatePropertyDefinitions(clrType).OrderBy(x => x.Name));

            foreach (var subType in ReflectionHelper.FindAllOfType(clrType).Except(new[]
                         {
                             clrType
                         })
                         .Where(_options.TypeFilter)) {
                MapType(subType);
            }

            return rpcType;
        }

        private IRpcType MapUnionType(Type clrType)
        {
            var memberTypes = new List<IRpcType>();
            var type = new UnionRpcType(memberTypes, clrType);
            _mappings.Add(clrType, type);
            memberTypes.AddRange(ReflectionHelper.FindAllOfType(clrType).Except(new[]
                {
                    clrType
                })
                .Where(_options.TypeFilter).Select(MapType));
            return type;
        }

        private IRpcType MapEnum(Type clrType)
        {
            var enumType = new EnumRpcType(_options.TypeNameFormatter(clrType), clrType,
                Enum.GetNames(clrType).Zip(Enum.GetValues(clrType).Cast<int>(),
                    (name, val) => new RpcEnumMember(_options.EnumMemberNameFormatter(name), val)).ToList());
            _types.Add(enumType);
            _mappings.Add(clrType, enumType);
            return enumType;
        }

        private IEnumerable<RpcPropertyDefinition> CreatePropertyDefinitions(Type clrType)
        {
            var memberInfos = clrType.GetMembers(_options.MemberBindingFilter).Where(_options.MemberFilter);

            var props = new List<RpcPropertyDefinition>();


            foreach (var memberInfo in memberInfos) {
                switch (memberInfo) {
                    case FieldInfo fieldInfo when _options.TypeFilter(fieldInfo.FieldType):
                    {
                        var propertyInfoPropertyType = fieldInfo.FieldType;
                        var tsProperty = CreatePropertyModel(propertyInfoPropertyType, fieldInfo);
                        props.Add(tsProperty);
                        break;
                    }

                    case PropertyInfo propertyInfo when _options.TypeFilter(propertyInfo.PropertyType):
                    {
                        var propertyInfoPropertyType = propertyInfo.PropertyType;
                        var tsProperty = CreatePropertyModel(propertyInfoPropertyType, propertyInfo);
                        props.Add(tsProperty);
                        break;
                    }
                }
            }

            return props;

            RpcPropertyDefinition CreatePropertyModel(Type memberType, MemberInfo memberInfo)
            {
                var type =
                    _options.IsMemberNullable(memberInfo) ? MakeNullable(MapType(memberType)) : MapType(memberType);
                var propertyDefinition =
                    new RpcPropertyDefinition(_options.MemberNameFormatter(memberInfo), type, memberInfo)
                    {
                        IsOptional = _options.IsMemberOptional(memberInfo),
                    };
                return propertyDefinition;
            }
        }

        private static UnionRpcType MakeNullable(IRpcType innerType)
        {
            return new UnionRpcType(new[]
            {
                PrimitiveTypes.Null, innerType
            }, innerType.ClrType);
        }

        public RpcModel Build()
        {
            return new RpcModel(_types, _services.Values);
        }
    }
}