using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CookeRpc.AspNetCore.Model.TypeDefinitions;
using CookeRpc.AspNetCore.Model.Types;
using CookeRpc.AspNetCore.Utils;

namespace CookeRpc.AspNetCore.Model
{
    public class RpcModel
    {
        private readonly RpcModelOptions _options;
        private readonly List<RpcTypeDefinition> _typesDefinitions;
        private readonly Dictionary<Type, RpcType> _typesByClrType;
        private readonly Dictionary<string, RpcType> _typesByName;
        private readonly List<RpcServiceModel> _services = new();

        public RpcModel() : this(new RpcModelOptions())
        {
        }

        public RpcModel(RpcModelOptions options)
        {
            _options = options;
            _typesByClrType = options.InitialTypeMap.ToDictionary(x => x.Key, x => x.Value);
            _typesByName = _typesByClrType.Values.Where(x => x.Name is not null).Distinct().ToDictionary(x => x.Name!);
            _typesDefinitions = options.InitialTypeDefinitions.ToList();
        }

        public IReadOnlyCollection<RpcTypeDefinition> TypesDefinitions => _typesDefinitions;

        public IReadOnlyCollection<RpcServiceModel> Services => _services;

        public RpcType AddType(Type type)
        {
            if (!_options.TypeFilter(type))
            {
                throw new InvalidOperationException("Type cannot be added due to the type filter");
            }

            if (_typesByClrType.TryGetValue(type, out var tsType))
            {
                return tsType;
            }

            var genericDictionary = ReflectionHelper.GetGenericTypeOfDefinition(type, typeof(IDictionary<,>)) ??
                                    ReflectionHelper.GetGenericTypeOfDefinition(type, typeof(IReadOnlyDictionary<,>));
            if (genericDictionary != null)
            {
                var keyType = genericDictionary.GenericTypeArguments[0];
                var valueType = genericDictionary.GenericTypeArguments[1];
                return new GenericType(NativeType.Map, new[] {AddType(keyType), AddType(valueType)});
            }

            var genericArray = ReflectionHelper.GetGenericTypeOfDefinition(type, typeof(IEnumerable<>));
            if (genericArray != null)
            {
                return new GenericType(NativeType.Array, new[] {AddType(genericArray.GenericTypeArguments[0])});
            }

            var genericNullable = ReflectionHelper.GetGenericTypeOfDefinition(type, typeof(Nullable<>));
            if (genericNullable != null)
            {
                return new UnionType(new[] {NativeType.Null, AddType(genericNullable.GetGenericArguments().Single())});
            }

            var optional = ReflectionHelper.GetGenericTypeOfDefinition(type, typeof(Optional<>));
            if (optional != null)
            {
                return new GenericType(NativeType.Optional, new[] {AddType(optional.GetGenericArguments().Single())});
            }

            if (type.IsInterface)
            {
                return AddUnion(type);
            }

            if (type.IsClass)
            {
                if (type.IsAbstract)
                {
                    return AddUnion(type);
                }

                return AddContract(type);
            }

            if (type.IsEnum)
            {
                return AddEnum(type);
            }

            throw new ArgumentException($"Invalid type {type}");
        }

        private RpcType AddType(RpcTypeDefinition typeDefinition)
        {
            var customType = new CustomType(typeDefinition);

            _typesDefinitions.Add(typeDefinition);
            _typesByClrType.Add(typeDefinition.ClrType, customType);
            _typesByName.Add(typeDefinition.Name, customType);

            return customType;
        }

        public RpcType AddScalarType(Type type, RpcType implementationType)
        {
            return AddType(new RpcScalarDefinition(_options.TypeNameFormatter(type), type, implementationType));
        }

        private RpcType AddContract(Type type)
        {
            var props = new List<RpcPropertyDefinition>();
            var extenders = new List<RpcType>();
            var typeDefinition = new RpcComplexDefinition(_options.TypeNameFormatter(type), type, props, extenders);
            var customType = new CustomType(typeDefinition);

            _typesDefinitions.Add(typeDefinition);
            _typesByClrType.Add(typeDefinition.ClrType, customType);
            _typesByName.Add(typeDefinition.Name, customType);

            extenders.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[] {type}).Where(_options.TypeFilter)
                .Select(AddContract));
            props.AddRange(CreatePropertyDefinitions(type).OrderBy(x => x.Name));

            return customType;
        }

        private RpcType AddUnion(Type type)
        {
            var memberTypes = new List<RpcType>();
            var name = _options.TypeNameFormatter(type);
            var typeDefinition = new RpcUnionDefinition(name, type, memberTypes);
            var customType = new CustomType(typeDefinition);

            _typesDefinitions.Add(typeDefinition);
            _typesByClrType.Add(typeDefinition.ClrType, customType);

            memberTypes.AddRange(ReflectionHelper.FindAllOfType(type).Except(new[] {type}).Where(_options.TypeFilter)
                .Select(AddType));

            return customType;
        }

        private RpcType AddEnum(Type type)
        {
            var typeDefinition = new RpcEnumDefinition(_options.TypeNameFormatter(type), type,
                Enum.GetNames(type).Zip(Enum.GetValues(type).Cast<int>(),
                    (name, val) => new RpcEnumMember(_options.EnumMemberNameFormatter(name), val)).ToList());
            var customType = new CustomType(typeDefinition);

            _typesDefinitions.Add(typeDefinition);
            _typesByClrType.Add(typeDefinition.ClrType, customType);
            _typesByName.Add(typeDefinition.Name, customType);

            return customType;
        }

        private List<RpcPropertyDefinition> CreatePropertyDefinitions(Type type)
        {
            var memberInfos = type.GetMembers(_options.MemberBindingFilter).Where(_options.MemberFilter);

            var props = new List<RpcPropertyDefinition>();

            RpcPropertyDefinition CreatePropertyModel(Type propertyInfoPropertyType1, MemberInfo memberInfo)
            {
                var propTypeRef = AddType(propertyInfoPropertyType1);
                var propertyDefinition = new RpcPropertyDefinition(_options.MemberNameFormatter(memberInfo),
                    _options.IsMemberNullable(memberInfo)
                        ? new UnionType(new[] {NativeType.Null, propTypeRef})
                        : propTypeRef, memberInfo) {IsOptional = _options.IsMemberOptional(memberInfo),};
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

        public void AddService(Type rpcControllerType)
        {
            var serviceName = _options.ServiceNameFormatter(rpcControllerType);

            var procedures = new List<RpcProcedureModel>();
            var serviceModel = new RpcServiceModel(serviceName, procedures);

            var methods = rpcControllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.DeclaringType != typeof(object));

            foreach (var method in methods)
            {
                var (rpcDelegate, parameterInfos, returnType) = RpcDelegateFactory.Create(method);

                List<RpcParameterModel> rpcParameterModels = new();
                foreach (var parameterInfo in parameterInfos)
                {
                    var paraType = AddType(parameterInfo.ParameterType);
                    var paraModel = new RpcParameterModel(parameterInfo.Name ?? throw new InvalidOperationException(),
                        paraType, parameterInfo.HasDefaultValue);

                    rpcParameterModels.Add(paraModel);
                }

                var rpcReturnType = AddType(returnType);
                var returnTypeModel = ReflectionHelper.IsNullableReturn(method)
                    ? new UnionType(new[] {NativeType.Null, rpcReturnType})
                    : rpcReturnType;

                var procModel = new RpcProcedureModel(method.Name, rpcDelegate, returnTypeModel, rpcParameterModels);

                procedures.Add(procModel);
            }

            _services.Add(serviceModel);
        }

        public RpcType? GetType(string typeName)
        {
            return _typesByName.GetValueOrDefault(typeName);
        }

        public RpcType? GetType(Type clrType)
        {
            return _typesByClrType.GetValueOrDefault(clrType);
        }
    }
}