using Hydra.Core.Sharding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.Data.Entities
{
    public class ULNHelper
    {
        public Dictionary<long, int> GetAllULNs()
        {
            var whereList = new List<long>()
            {
                1000000027,
1000000035,
1000000043,
1000000051,
1000000078,
1000000272,
1000000280,
1000000299,
1000000302,
1000000310,
1000000477,
1000000485,
1000000493,
1000000647,
1000000655,
1000000671,
1000000779,
1000000787,
1000000795
            };

            //get data from db
            using (var ulnContext = new DCT.ULN.Model.ULNv2())
            {
                return ulnContext.UniqueLearnerNumbers2
                          //.Where(x=> whereList.Contains(x.ULN))
                          .Select(uln => uln.ULN)
                          //.Take(100)
                          .ToDictionary(uln => uln, uln => new JumpSharding().GetShard(uln.ToString(), 10));
            }
        }
    }
}
