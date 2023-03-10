using System;

namespace CookeRpc.AspNetCore.Model.TypeDefinitions
{
    public abstract class RpcType
    {
        protected RpcType(Type clrType)
        {
            ClrType = clrType;
        }

        public Type ClrType { get; set; }
    }

    public class RpcRefType : RpcType
    {
        public RpcTypeDeclaration ReferencedType { get; }
        
        public RpcRefType(RpcTypeDeclaration referencedType) : base(referencedType.Type.ClrType)
        {
            ReferencedType = referencedType;
        }
    }
}