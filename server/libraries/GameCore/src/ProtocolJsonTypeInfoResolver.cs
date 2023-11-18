using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace GameCore
{
    sealed class ProtocolJsonTypeInfoResolver : DefaultJsonTypeInfoResolver
    {
        static readonly Type BaseType = typeof(ProtocolBase);
        static readonly JsonPolymorphismOptions JsonPolymorphismOptions;

        static ProtocolJsonTypeInfoResolver()
        {
            var options = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$protocol-type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };
            var query = BaseType.Assembly.GetExportedTypes()
                .Where(BaseType.IsAssignableFrom)
                .Where(t => !t.IsAbstract)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => new JsonDerivedType(t, t.Namespace + "." + t.Name));
            foreach (var type in query)
                options.DerivedTypes.Add(type);
            JsonPolymorphismOptions = options;
        }

        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var info = base.GetTypeInfo(type, options);
            if (info.Type == BaseType)
                info.PolymorphismOptions = JsonPolymorphismOptions;
            return info;
        }
    }

}