using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models.Models;

namespace DCT.ILR.ValidationService.Models
{
    public static class ConfigurationHelper 
    {
        public static T GetSectionValues<T>(string sectionName)
        {

            if (sectionName == null)
                throw new ArgumentNullException(nameof(sectionName));

            //get the section parameters using Fabricruntime
            var sectionConfigParameters = FabricRuntime.GetActivationContext()
                .GetConfigurationPackageObject("Config")
                .Settings
                .Sections[sectionName].Parameters;

            //create instance of T
            var returnObject = (T)Activator.CreateInstance(typeof(T));

            //get all properties of T and match with parameters from Config section and populate it.
            PropertyInfo[] configOptionsProperties = typeof(T).GetProperties();

            foreach (var configurationProperty in sectionConfigParameters)
            {
                var configOptionsProperty = configOptionsProperties.FirstOrDefault(x => x.Name == configurationProperty.Name);
                if (configOptionsProperty != null)
                {
                    configOptionsProperty.SetValue(returnObject, configurationProperty.Value);
                }
            }
            return returnObject;
        }
    }
}
