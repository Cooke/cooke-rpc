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
    public record RpcModel(IReadOnlyCollection<RpcTypeDeclaration> TypeDeclarations,
        IReadOnlyCollection<RpcServiceModel> Services);

    public record RpcTypeDeclaration(string Name, RpcType Type);

    public class RpcModelBuilder
    {
        private readonly RpcModelBuilderOptions _options;
        private readonly List<RpcTypeDeclaration> _typeDeclarations;
        private readonly Dictionary<Type, RpcType> _typeMap;
        private readonly Dictionary<Type, RpcServiceModel> _services = new();

        public RpcModelBuilder() : this(new RpcModelBuilderOptions())
        {
        }

        public RpcModelBuilder(RpcModelBuilderOptions options)
        {
            _options = options;
            _typeDeclarations = new();
            _typeMap = new();
        }

        public RpcType MapType(Type clrType)
        {
            var knownType = _typeMap.GetValueOrDefault(clrType);
            if (knownType != null) {
                return knownType;
            }

            if (_options.PrimitiveTypeMap.TryGetValue(clrType, out var primitiveType)) {
                return DeclareType(primitiveType.Name, primitiveType);
            }

            if (_options.OnAddingType != null) {
                _options.OnAddingType.Invoke(clrType, this);

                var knownType2 = _typeMap.GetValueOrDefault(clrType);
                if (knownType2 != null) {
                    return knownType2;
                }
            }

            if (!_options.TypeFilter(clrType)) {
                throw new InvalidOperationException(
                    $"Type ${clrType} cannot be mapped due to the type filter configuration");
            }

            if (clrType.GetCustomAttribute<RpcTypeAttribute>()?.Kind == RpcTypeKind.Union) {
                return AddUnionType(clrType);
            }

            var genericDictionary = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IDictionary<,>)) ??
                                    ReflectionHelper.GetGenericTypeOfDefinition(clrType,
                                        typeof(IReadOnlyDictionary<,>));
            if (genericDictionary != null) {
                var keyType = genericDictionary.GenericTypeArguments[0];
                var valueType = genericDictionary.GenericTypeArguments[1];
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Map, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.AddRange(new[] { MapType(keyType), MapType(valueType) });
                return genericType;
            }

            var genericClrArray = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IEnumerable<>));
            if (genericClrArray != null) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Array, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(MapType(genericClrArray.GenericTypeArguments[0]));
                return genericType;
            }

            var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Nullable<>));
            if (genericNullable != null) {
                var typeArguments = new List<RpcType> { PrimitiveTypes.Null };
                var unionType = new RpcUnionType(typeArguments, clrType);
                _typeMap.Add(clrType, unionType);
                typeArguments.Add(MapType(genericNullable.GetGenericArguments().Single()));
                return unionType;
            }

            var optional = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Optional<>));
            if (optional != null) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Optional, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(MapType(optional.GetGenericArguments().Single()));
                return genericType;
            }

            if (clrType.IsAssignableTo(typeof(ITuple))) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Tuple, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.AddRange(clrType.GenericTypeArguments.Select(MapType));
                return genericType;
            }

            if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() != clrType) {
                return MapType(clrType.GetGenericTypeDefinition());
            }

            if (clrType.IsClass || clrType.IsInterface) {
                return DeclareObject(clrType);
            }

            if (clrType.IsEnum) {
                return DeclareUnion(clrType);
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
                    var paraType = ReflectionHelper.IsNullable(parameterInfo)
                        ? new RpcUnionType(new[] { PrimitiveTypes.Null, MapType(parameterInfo.ParameterType) },
                            parameterInfo.ParameterType)
                        : MapType(parameterInfo.ParameterType);

                    var paraModel = new RpcParameterModel(parameterInfo.Name ?? throw new InvalidOperationException(),
                        paraType, parameterInfo.HasDefaultValue);

                    rpcParameterModels.Add(paraModel);
                }

                var rpcReturnType = MapType(returnType);
                var returnTypeModel = ReflectionHelper.IsNullableReturn(method)
                    ? new RpcUnionType(new[] { PrimitiveTypes.Null, rpcReturnType }, method.ReturnType)
                    : rpcReturnType;

                var procModel = new RpcProcedureModel(_options.ProcedureNameFormatter(method), rpcDelegate,
                    returnTypeModel, rpcParameterModels, method.GetCustomAttributes().ToArray());

                procedures.Add(procModel);
            }

            _services.Add(serviceType, serviceModel);
            return serviceModel;
        }

        private RpcRefType DeclareType(String name, RpcType type)
        {
            var rpcTypeDeclaration = new RpcTypeDeclaration(name, type);
            _typeDeclarations.Add(rpcTypeDeclaration);
            _typeMap.Add(type.ClrType, new RpcRefType(rpcTypeDeclaration));
            return new RpcRefType(rpcTypeDeclaration);
        }

        private RpcType DeclareObject(Type type)
        {
            string typeName = _options.TypeNameFormatter(type);
            var props = new List<RpcPropertyDefinition>();
            var extends = new List<RpcType>(); // Can only be reffed object type or generic type
            var rpcType = DeclareType(typeName,
                new RpcObjectType(type, props, extends, type.IsAbstract || type.IsInterface));

            if (type.BaseType != typeof(object) && type.BaseType != null && _options.TypeFilter(type.BaseType)) {
                var rpcBaseType = MapType(type.BaseType);
                extends.Add(rpcBaseType);
            }

            foreach (var @interface in type.GetInterfaces().Where(_options.InterfaceFilter)) {
                var interfaceRpcType = MapType(@interface);
                extends.Add(interfaceRpcType);
            }

            props.AddRange(CreatePropertyDefinitions(type).OrderBy(x => x.Name));

            foreach (var subType in ReflectionHelper.FindAllOfType(type).Except(new[] { type })
                         .Where(_options.TypeFilter)) {
                MapType(subType);
            }

            return rpcType;
        }

        private RpcType AddUnionType(Type type)
        {
            var memberTypes = new List<RpcType>();
            var rpcType = new RpcUnionType(memberTypes, type);
            _typeMap.Add(type, rpcType);
            memberTypes.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[] { type }).Where(_options.TypeFilter)
                .Select(MapType));
            return rpcType;
        }

        private RpcType DeclareUnion(Type type)
        {
            var enumType = new RpcEnum(type,
                Enum.GetNames(type).Zip(Enum.GetValues(type).Cast<int>(),
                    (name, val) => new RpcEnumMember(_options.EnumMemberNameFormatter(name), val)).ToList());
            return DeclareType(_options.TypeNameFormatter(type), enumType);
        }

        private IEnumerable<RpcPropertyDefinition> CreatePropertyDefinitions(Type type)
        {
            var memberInfos = type.GetMembers(_options.MemberBindingFilter).Where(_options.MemberFilter);

            var props = new List<RpcPropertyDefinition>();

            RpcPropertyDefinition CreatePropertyModel(Type memberType, MemberInfo memberInfo)
            {
                var propTypeRef = MapType(memberType);
                var propertyDefinition = new RpcPropertyDefinition(_options.MemberNameFormatter(memberInfo),
                    _options.IsMemberNullable(memberInfo)
                        ? new RpcUnionType(new[] { PrimitiveTypes.Null, propTypeRef }, memberType)
                        : propTypeRef, memberInfo)
                {
                    IsOptional = _options.IsMemberOptional(memberInfo),
                };
                return propertyDefinition;
            }

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
        }

        public RpcModel Build()
        {
            return new RpcModel(_typeDeclarations, _services.Values);
        }
    }
}