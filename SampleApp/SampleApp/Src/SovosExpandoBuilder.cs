using System;
using System.Collections.Generic;
using Sovos.Infrastructure;

namespace SampleApp
{
  public class SovosExpandoBuilder
  {

    public static SovosExpando Build()
    {
      var expando = new SovosExpando();
      expando.Dictionary.Add("GetTest", new Func<IDictionary<string, object>, string>(_this =>
      {
        if (!_this.ContainsKey("_test"))
          _this.Add("_test", "");
        return (string) _this["_test"];
      }));
      expando.Dictionary.Add("SetTest", new Action<IDictionary<string, object>, string>((_this, value) =>
      {
        _this["_test"] = value;
      }));
      expando.Dictionary.Add("ResetTest", new Action<IDictionary<string, object>>(_this =>
      {
        _this["_test"] = "";
      }));
      return expando;
    }
  }
}