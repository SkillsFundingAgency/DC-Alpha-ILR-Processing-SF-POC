using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.Models.Models
{
    public class IlrContext
    {
        public string Filename;
        public string ContainerReference;
        public Guid CorrelationId;
        public bool IsShredAndProcess;
    }
}
