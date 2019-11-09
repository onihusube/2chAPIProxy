using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using _2chAPIProxy.APIMediator;
using _2chAPIProxy.HtmlConverter;

namespace _2chAPIProxy.Models
{
    public interface IModelFuctory
    {
        APIMediator.IAPIMediator CreateAPIMediator();

        HtmlConverter.IHtmlConverter CreateHtmlConverter();
    }

    public class ModelFuctory : IModelFuctory
    {
        public IAPIMediator CreateAPIMediator()
        {
            return APIMediator.Fuctory.GetSingletonInstance();
        }

        public IHtmlConverter CreateHtmlConverter()
        {
            return HtmlConverter.Fuctory.GetSingletonInstance();
        }
    }
}
