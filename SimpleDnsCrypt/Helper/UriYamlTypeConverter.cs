using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace SimpleDnsCrypt.Helper
{
    internal sealed class UriYamlTypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type == typeof(Uri);
        }

        public object ReadYaml(IParser parser, Type type)
        {
            var value = ((Scalar)parser.Current).Value;
            parser.MoveNext();
            return new Uri(value);
        }

        public void WriteYaml(IEmitter emitter, object value, Type type)
        {
            var uri = (Uri)value;
            emitter.Emit(new Scalar(null, null, uri.ToString(), ScalarStyle.Any, true, false));
        }
    }
}
