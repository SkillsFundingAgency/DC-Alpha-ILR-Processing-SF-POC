using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DCT.ILR.ValidationService.Models.Models
{
    public class ActorsHelper : IActorsHelper
    {
        public int GetLearnersPerActor(int totalMessagesCount)
        {

            if (totalMessagesCount <= 500)
            {
                return 100;
            }
            if (totalMessagesCount <= 1000)
            {
                return 250;
            }
            if (totalMessagesCount <= 10000)
            {
                return 1000;
            }
            if (totalMessagesCount <= 30000)
            {
                return 5000;
            }

            return 10000;
        }
    }
}
