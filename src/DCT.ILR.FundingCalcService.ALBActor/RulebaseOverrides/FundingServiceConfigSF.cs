using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCT.FundingService.Config.Interface;

namespace DCT.ILR.FundingCalcService.ALBActor.RulebaseOverrides
{
    public class FundingServiceConfigSF : IFundingServiceConfig
    {
        public bool XdsGeneration => false;
        public string xDSLoggingPath => @"C:\Code\temp\";

        public string rulebasePath => Path.Combine(
            FabricRuntime.GetActivationContext().GetCodePackageObject("Code").Path,
            "lib","Rulebases","Loans Bursary 17_18.zip");

        public string xsltPath => @"C:\Code\FundingService\src\DCT.FundingService.Service\Artefacts\ILR 24+ Loans Calculation - Input_XSLT.xsl";
        public int fundModel => 99;
        public bool persistToXml => true;
        public bool persistToSql => false;

        public Dictionary<int, DateTime> periods => new Dictionary<int, DateTime>
        {
            { 1, new DateTime(2017, 08,01)},
            { 2, new DateTime(2017, 09,01)},
            { 3, new DateTime(2017, 10,01)},
            { 4, new DateTime(2017, 11,01)},
            { 5, new DateTime(2017, 12,01)},
            { 6, new DateTime(2018, 01,01)},
            { 7, new DateTime(2018, 02,01)},
            { 8, new DateTime(2018, 03,01)},
            { 9, new DateTime(2018, 04,01)},
            { 10, new DateTime(2018, 05,01)},
            { 11, new DateTime(2018, 06,01)},
            { 12, new DateTime(2018, 07,01)},
        };
    }
}
