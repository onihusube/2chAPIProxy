using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using _2chAPIProxy.HtmlConverter;

namespace UnitTestProject1
{
    [TestClass]
    public class HtmlConverterTest
    {

        [TestMethod]
        public void ConvertTest()
        {
            var converter = new HtmltoDat();

            converter.UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64; Trident/7.0; Touch; rv:11.0) like Gecko";
            converter.IsAliveCheckSkip = true;
            converter.IsExternalConverterUse = false;

            var dat = converter.Gethtml("https://asahi.5ch.net/test/read.cgi/newsplus/1543674544/", -1, "", false);

            using (var sw = new StreamWriter(@"D:\\convert.dat", false, Encoding.GetEncoding(932)))
            {
                sw.BaseStream.Write(dat, 0, dat.Length);
            }
        }
    }
}
