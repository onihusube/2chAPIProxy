using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _2chAPIProxy.HtmlConverter
{
    public static class Fuctory
    {
        /// <summary>
        /// 既定のIHtmlConverter実装を導出する
        /// </summary>
        /// <returns>呼び出し毎に生成されるIHtmlConverter</returns>
        public static IHtmlConverter Create()
        {
            return new HtmltoDat();
        }

        private static readonly IHtmlConverter singleton = new HtmltoDat();
        /// <summary>
        /// 既定のIHtmlConverter実装を導出する、常に同じオブジェクトを返す
        /// </summary>
        /// <returns>IHtmlConverter</returns>
        public static IHtmlConverter GetSingletonInstance()
        {
            return singleton;
        }
    }
}
