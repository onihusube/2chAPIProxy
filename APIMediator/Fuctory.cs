using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace _2chAPIProxy.APIMediator
{
    public static class Fuctory
    {
        /// <summary>
        /// 既定のIAPIMediator実装を導出する
        /// </summary>
        /// <returns>呼び出し毎に生成されるIAPIMediator</returns>
        public static IAPIMediator Create()
        {
            return new APIAccess();
        }

        private static readonly IAPIMediator singleton = new APIAccess();
        /// <summary>
        /// 既定のIAPIMediator実装を導出する、常に同じオブジェクトを返す
        /// </summary>
        /// <returns>IAPIMediator</returns>
        public static IAPIMediator GetSingletonInstance()
        {
            return singleton;
        }
    }
}
