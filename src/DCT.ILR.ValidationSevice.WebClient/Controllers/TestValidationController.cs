using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DCT.ILR.ValidationService.Models;
using DCT.ILR.ValidationService.Models.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Remoting.Client;

namespace DCT.ILR.ValidationSevice.WebClient.Controllers
{
    [Produces("application/json")]
    [Route("api/TestValidation")]
    public class TestValidationController : Controller
    {
        [HttpPost]
        public async Task<bool> Post()
        {
            var clientProxy = ServiceProxy.Create<IValidationServiceStateful>(
                new Uri("fabric:/DCT.ILR.Processing.POC/DCT.ILR.VadationServiceStateful"),
                new ServicePartitionKey(LongRandom()));

            try
            {
                var result = await clientProxy.Validate(new IlrContext()
                {
                    Filename = "ILR-10006341-1718-20171107-113456-02.xml",
                    ContainerReference = "ilr"
                });

                return true;
            }
            catch (Exception ex)
            {

                throw;
            }
          
        }

        private long LongRandom()
        {
            byte[] buf = new byte[8];
            new Random().NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);
            return longRand;
        }
    }
}