using DCT.ULN.Model.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.Data.ULNv2.Entities
{
    public class ULNHelper
    {
        public IEnumerable<long> GetAllULNs()
        {
            //get data from db
            using (var ulnContext = new DCT.ULN.Model.ULNv2())
            {
                return ulnContext.UniqueLearnerNumbers2
                          .Select(uln => uln.ULN).ToList();               
            }
        }
    }
}
