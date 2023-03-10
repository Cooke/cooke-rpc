using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public record RpcModel(IReadOnlyCollection<RpcTypeDeclaration> TypeDeclarations, IReadOnlyCollection<RpcServiceModel> Services);

    public record RpcTypeDeclaration(string Name, RpcType Type);

    public class RpcModelBuilder
    {
        private readonly RpcModelBuilderOptions _builderOptions;
        private readonly List<RpcTypeDeclaration> _typeDeclarations;
        private readonly Dictionary<Type, RpcType> _typeMap;
        private readonly Dictionary<Type, RpcServiceModel> _services = new();

        public RpcModelBuilder() : this(new RpcModelBuilderOptions())
        {
        }

        public RpcModelBuilder(RpcModelBuilderOptions builderOptions)
        {
            _builderOptions = builderOptions;
            _typeDeclarations = builderOptions.InitialTypeDeclarations.ToList();
            _typeMap = builderOptions.InitialTypeMap.ToDictionary(x => x.Key, x => x.Value);
        }

        public RpcType AddType(Type clrType)
        {
            var knownType = _typeMap.GetValueOrDefault(clrType);
            if (knownType != null) {
                return knownType;
            }

            if (_builderOptions.OnAddingType != null) {
                _builderOptions.OnAddingType.Invoke(clrType, this);

                var knownType2 = _typeMap.GetValueOrDefault(clrType);
                if (knownType2 != null) {
                    return knownType2;
                }
            }

            if (!_builderOptions.TypeFilter(clrType)) {
                throw new InvalidOperationException("Type cannot be mapped due to the type filter configuration");
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
                typeArguments.AddRange(new[]
                {
                    AddType(keyType), AddType(valueType)
                });
                return genericType;
            }

            var genericClrArray = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(IEnumerable<>));
            if (genericClrArray != null) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Array, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(AddType(genericClrArray.GenericTypeArguments[0]));
                return genericType;
            }

            var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Nullable<>));
            if (genericNullable != null) {
                var typeArguments = new List<RpcType>
                {
                    PrimitiveTypes.Null
                };
                var unionType = new RpcUnionType(typeArguments, clrType);
                _typeMap.Add(clrType, unionType);
                typeArguments.Add(AddType(genericNullable.GetGenericArguments().Single()));
                return unionType;
            }

            var optional = ReflectionHelper.GetGenericTypeOfDefinition(clrType, typeof(Optional<>));
            if (optional != null) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Optional, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.Add(AddType(optional.GetGenericArguments().Single()));
                return genericType;
            }

            if (clrType.IsAssignableTo(typeof(ITuple))) {
                var typeArguments = new List<RpcType>();
                var genericType = new RpcGenericType(clrType, PrimitiveTypes.Tuple, typeArguments);
                _typeMap.Add(clrType, genericType);
                typeArguments.AddRange(clrType.GenericTypeArguments.Select(AddType));
                return genericType;
            }

            if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() != clrType) {
                return AddType(clrType.GetGenericTypeDefinition());
            }

            if (clrType.IsInterface || clrType.IsAbstract) {
                return AddUnionType(clrType);
            }

            if (clrType.IsClass) {
                return AddObject(clrType);
            }

            if (clrType.IsEnum) {
                return AddEnum(clrType);
            }

            throw new ArgumentException($"Invalid type {clrType}");
        }

        // public RpcType MapType(Type clrType, RpcType rpcType)
        // {
        //     var knownType = _typeMap.GetValueOrDefault(clrType);
        //     if (knownType != null && rpcType == knownType) {
        //         return knownType;
        //     }
        //
        //     if (knownType != null) {
        //         throw new InvalidOperationException($"Type {clrType} is already mapped to {knownType}");
        //     }
        //
        //     if (rpcType is RpcPrimitiveType r && !_typesDefinitions.Contains(r.TypeDefinition)) {
        //         _typesDefinitions.Add(r.TypeDefinition);
        //     }
        //
        //     _typeMap.Add(clrType, rpcType);
        //     return rpcType;
        // }

        public RpcServiceModel AddService(Type serviceType)
        {
            if (_services.ContainsKey(serviceType)) {
                return _services[serviceType];
            }

            var serviceName = _builderOptions.ServiceNameFormatter(serviceType);

            var procedures = new List<RpcProcedureModel>();
            var serviceModel = new RpcServiceModel(serviceType, serviceName, procedures, serviceType.GetCustomAttributes().ToArray());

            var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.DeclaringType != typeof(object));

            foreach (var method in methods) {
                var (rpcDelegate, parameterInfos, returnType) = RpcDelegateFactory.Create(method, _builderOptions.ContextType,
                    _builderOptions.CustomParameterResolver);

                List<RpcParameterModel> rpcParameterModels = new();
                foreach (var parameterInfo in parameterInfos) {
                    var paraType = ReflectionHelper.IsNullable(parameterInfo)
                        ? new RpcUnionType(new[]
                        {
                            PrimitiveTypes.Null, AddType(parameterInfo.ParameterType)
                        }, parameterInfo.ParameterType)
                        : AddType(parameterInfo.ParameterType);

                    var paraModel = new RpcParameterModel(parameterInfo.Name ?? throw new InvalidOperationException(),
                        paraType, parameterInfo.HasDefaultValue);

                    rpcParameterModels.Add(paraModel);
                }

                var rpcReturnType = AddType(returnType);
                var returnTypeModel = ReflectionHelper.IsNullableReturn(method)
                    ? new RpcUnionType(new[]
                    {
                        PrimitiveTypes.Null, rpcReturnType
                    }, method.ReturnType)
                    : rpcReturnType;

                var procModel = new RpcProcedureModel(_builderOptions.ProcedureNameFormatter(method), rpcDelegate,
                    returnTypeModel, rpcParameterModels, method.GetCustomAttributes().ToArray());

                procedures.Add(procModel);
            }

            _services.Add(serviceType, serviceModel);
            return serviceModel;
        }

        private RpcRefType AddTypeDeclaration(String name, RpcType type)
        {
            var rpcTypeDeclaration = new RpcTypeDeclaration(name, type);
            _typeDeclarations.Add(rpcTypeDeclaration);
            _typeMap.Add(type.ClrType, type);
            return new RpcRefType(rpcTypeDeclaration);
        }

        private RpcType AddObject(Type type)
        {
            var props = new List<RpcPropertyDefinition>();
            var extends = new List<RpcType>();
            var typeName = _builderOptions.TypeNameFormatter(type);
            var rpcType = AddTypeDeclaration(typeName, new RpcObject(type, props, extends));

            if (type.BaseType != typeof(object) && type.BaseType != null) {
                var rpcBaseType = AddType(type.BaseType);
                extends.Add(rpcBaseType);
            }

            foreach (var @interface in type.GetInterfaces().Where(_builderOptions.InterfaceFilter)) {
                var interfaceRpcType = AddType(@interface);
                extends.Add(interfaceRpcType);
            }

            props.AddRange(CreatePropertyDefinitions(type).OrderBy(x => x.Name));

            foreach (var subType in ReflectionHelper.FindAllOfType(type).Except(new[]
                         {
                             type
                         }
                     ).Where(_builderOptions.TypeFilter)) {
                AddType(subType);
            }

            return rpcType;
        }

        private RpcType AddUnionType(Type type)
        {
            var memberTypes = new List<RpcType>();
            var rpcType = new RpcUnionType(memberTypes, type);
            _typeMap.Add(type, rpcType);
            memberTypes.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[]
                {
                    type
                }).Where(_builderOptions.TypeFilter)
                .Select(AddType));
            return rpcType;
        }

        private RpcType AddEnum(Type type)
        {
            var enumType = new RpcEnum(type,
                Enum.GetNames(type).Zip(Enum.GetValues(type).Cast<int>(),
                    (name, val) => new RpcEnumMember(_builderOptions.EnumMemberNameFormatter(name), val)).ToList());
            return AddTypeDeclaration(_builderOptions.TypeNameFormatter(type), enumType);
        }

        private IEnumerable<RpcPropertyDefinition> CreatePropertyDefinitions(Type type)
        {
            var memberInfos = type.GetMembers(_builderOptions.MemberBindingFilter).Where(_builderOptions.MemberFilter);

            var props = new List<RpcPropertyDefinition>();

            RpcPropertyDefinition CreatePropertyModel(Type memberType, MemberInfo memberInfo)
            {
                var propTypeRef = AddType(memberType);
                var propertyDefinition = new RpcPropertyDefinition(_builderOptions.MemberNameFormatter(memberInfo),
                    _builderOptions.IsMemberNullable(memberInfo)
                        ? new RpcUnionType(new[]
                        {
                            PrimitiveTypes.Null, propTypeRef
                        }, memberType)
                        : propTypeRef, memberInfo)
                {
                    IsOptional = _builderOptions.IsMemberOptional(memberInfo),
                };
                return propertyDefinition;
            }

            foreach (var memberInfo in memberInfos) {
                switch (memberInfo) {
                    case FieldInfo fieldInfo when _builderOptions.TypeFilter(fieldInfo.FieldType):
                    {
                        var propertyInfoPropertyType = fieldInfo.FieldType;
                        var tsProperty = CreatePropertyModel(propertyInfoPropertyType, fieldInfo);
                        props.Add(tsProperty);
                        break;
                    }

                    case PropertyInfo propertyInfo when _builderOptions.TypeFilter(propertyInfo.PropertyType):
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