using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModel
    {
        private readonly RpcModelOptions _options;
        private readonly List<RpcTypeDefinition> _typesDefinitions = new();
        private readonly Dictionary<Type, RpcType> _typeMap = new();
        private readonly Dictionary<Type, RpcServiceModel> _services = new();

        public RpcModel() : this(new RpcModelOptions())
        {
        }

        public RpcModel(RpcModelOptions options)
        {
            _options = options;
        }

        public IReadOnlyCollection<RpcTypeDefinition> TypesDefinitions => _typesDefinitions;

        public IReadOnlyCollection<RpcServiceModel> Services => _services.Values;

        public IReadOnlyDictionary<Type, RpcType> MappedTypes => _typeMap;
        
        public RpcType MapType(Type clrType)
        {
            var knownType = _typeMap.GetValueOrDefault(clrType);
            if (knownType != null)
            {
                return knownType;
            }

            if (!_options.TypeFilter(clrType))
            {
                throw new InvalidOperationException("Type cannot be mapped due to the type filter configuration");
            }
            
            var customRpcType = _options.CustomTypeResolver(clrType);
            if (customRpcType != null)
            {
                _typeMap.Add(clrType, customRpcType);
                return customRpcType;
            }
            
            var customDefinition = _options.CustomTypeDefiner(clrType, this);
            if (customDefinition != null)
            {
                return AddTypeDefinition(customDefinition);
            }

            var defaultRpcType = _options.CustomDefaultTypeMap.GetValueOrDefault(clrType);
            if (defaultRpcType != null)
            {
                _typeMap.Add(clrType, defaultRpcType);
                return defaultRpcType;
            }

            var genericDictionary = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IDictionary<,>)) ??
                                    ReflectionHelper.GetGenericTypeOfDefinition(clrType,
                                        typeof(IReadOnlyDictionary<,>));
            if (genericDictionary != null)
            {
                var keyType = genericDictionary.GenericTypeArguments[0];
                var valueType = genericDictionary.GenericTypeArguments[1];
                var typeArguments = new List<RpcType>();
                var genericType = new GenericType(NativeType.Map,
                    typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.AddRange(new[] {MapType(keyType), MapType(valueType)});
                return genericType;
            }

            var genericClrArray = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IEnumerable<>));
            if (genericClrArray != null)
            {
                var typeArguments = new List<RpcType>();
                var genericType = new GenericType(NativeType.Array, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(MapType(genericClrArray.GenericTypeArguments[0]));
                return genericType;
            }

            var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Nullable<>));
            if (genericNullable != null)
            {
                var typeArguments = new List<RpcType> {NativeType.Null };
                var unionType = new UnionType(typeArguments);
                _typeMap.Add(clrType, unionType);
                typeArguments.Add(MapType(genericNullable.GetGenericArguments().Single()));
                return unionType;
            }

            var optional = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Optional<>));
            if (optional != null)
            {
                var typeArguments = new List<RpcType>();
                var genericType = new GenericType(NativeType.Optional, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(MapType(optional.GetGenericArguments().Single()));
                return genericType;
            }
            
            if (clrType.IsInterface)
            {
                return DefineUnion(clrType);
            }

            if (clrType.IsClass)
            {
                if (clrType.IsAbstract)
                {
                    return DefineUnion(clrType);
                }

                return DefineComplex(clrType);
            }

            if (clrType.IsEnum)
            {
                return DefineEnum(clrType);
            }

            if (clrType.IsAssignableTo(typeof(ITuple))) {
                var typeArguments = new List<RpcType>();
                var genericType = new GenericType(NativeType.Tuple, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.AddRange(clrType.GenericTypeArguments.Select(MapType));
                return genericType;
            }

            throw new ArgumentException($"Invalid type {clrType}");
        }
        
        public void AddService(Type rpcControllerType)
        {
            if (_services.ContainsKey(rpcControllerType))
            {
                return;
            }

            var serviceName = _options.ServiceNameFormatter(rpcControllerType);

            var procedures = new List<RpcProcedureModel>();
            var serviceModel = new RpcServiceModel(rpcControllerType, serviceName, procedures);

            var methods = rpcControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.DeclaringType != typeof(object));

            foreach (var method in methods)
            {
                var (rpcDelegate, parameterInfos, returnType) =
                    RpcDelegateFactory.Create(method, _options.ContextType, _options.CustomParameterResolver);

                List<RpcParameterModel> rpcParameterModels = new();
                foreach (var parameterInfo in parameterInfos)
                {
                    var paraType = ReflectionHelper.IsNullable(parameterInfo)
                        ? new UnionType(new[] {NativeType.Null, MapType(parameterInfo.ParameterType)})
                        : MapType(parameterInfo.ParameterType);

                    var paraModel = new RpcParameterModel(parameterInfo.Name ?? throw new InvalidOperationException(),
                        paraType,
                        parameterInfo.HasDefaultValue);

                    rpcParameterModels.Add(paraModel);
                }

                var rpcReturnType = MapType(returnType);
                var returnTypeModel = ReflectionHelper.IsNullableReturn(method)
                    ? new UnionType(new[] {NativeType.Null, rpcReturnType})
                    : rpcReturnType;

                var procModel = new RpcProcedureModel(_options.ProcedureNameFormatter(method), rpcDelegate, returnTypeModel, rpcParameterModels);

                procedures.Add(procModel);
            }

            _services.Add(rpcControllerType, serviceModel);
        }

        private RpcType AddTypeDefinition(RpcTypeDefinition typeDefinition)
        {
            var customType = new CustomType(typeDefinition);
            _typesDefinitions.Add(typeDefinition);
            _typeMap.Add(typeDefinition.ClrType, customType);
            return customType;
        }

        private RpcType DefineComplex(Type type)
        {
            var props = new List<RpcPropertyDefinition>();
            var extenders = new List<RpcType>();
            var typeDefinition = new RpcComplexDefinition(_options.TypeNameFormatter(type), type, props, extenders);
            var customType = AddTypeDefinition(typeDefinition);

            extenders.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[] {type}).Where(_options.TypeFilter)
                .Select(DefineComplex));
            props.AddRange(CreatePropertyDefinitions(type).OrderBy(x => x.Name));

            return customType;
        }

        private RpcType DefineUnion(Type type)
        {
            var memberTypes = new List<RpcType>();
            var name = _options.TypeNameFormatter(type);
            var typeDefinition = new RpcUnionDefinition(name, type, memberTypes);
            var customType = AddTypeDefinition(typeDefinition);
            memberTypes.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[] {type}).Where(_options.TypeFilter)
                .Select(MapType));
            return customType;
        }

        private RpcType DefineEnum(Type type)
        {
            var typeDefinition = new RpcEnumDefinition(_options.TypeNameFormatter(type),
                type,
                Enum.GetNames(type).Zip(Enum.GetValues(type).Cast<int>(),
                    (name, val) => new RpcEnumMember(_options.EnumMemberNameFormatter(name), val)).ToList());
            return AddTypeDefinition(typeDefinition);
        }

        private IEnumerable<RpcPropertyDefinition> CreatePropertyDefinitions(Type type)
        {
            var memberInfos = type.GetMembers(_options.MemberBindingFilter).Where(_options.MemberFilter);

            var props = new List<RpcPropertyDefinition>();

            RpcPropertyDefinition CreatePropertyModel(Type propertyInfoPropertyType1, MemberInfo memberInfo)
            {
                var propTypeRef = MapType(propertyInfoPropertyType1);
                var propertyDefinition = new RpcPropertyDefinition(_options.MemberNameFormatter(memberInfo),
                    _options.IsMemberNullable(memberInfo)
                        ? new UnionType(new[] {NativeType.Null, propTypeRef})
                        : propTypeRef,
                    memberInfo) {IsOptional = _options.IsMemberOptional(memberInfo),};
                return propertyDefinition;
            }

            foreach (var memberInfo in memberInfos)
            {
                switch (memberInfo)
                {
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
        }
    }
}