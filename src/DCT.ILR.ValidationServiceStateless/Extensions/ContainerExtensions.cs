using Autofac;
using Autofac.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationServiceStateless.Extensions
{
    public static class ContainerExtensions
    {
        public static T[] ResolveAllWithParameters<T>(this IContainer Container, IDictionary<string, object> parameters)
        {
            var _parameters = new List<Parameter>();
            foreach (var parameter in parameters)
            {
                _parameters.Add(new NamedParameter(parameter.Key, parameter.Value));
            }
            return Container.Resolve<IEnumerable<T>>(_parameters).ToArray();
        }
    }
}
