using System;
using Sovos.Infrastructure;

namespace SampleApp
{
  public class SovosExpandoBuilder
  {
    public static SovosExpando Build()
    {
      var expando = new SovosExpando();
      expando.Dictionary.Add("GetTest", new Func<SovosExpando, string>(_this =>
      {
        if (!_this.Dictionary.ContainsKey("_test"))
          _this.Dictionary.Add("_test", "");
        return (string) _this.Dictionary["_test"];
      }));
      expando.Dictionary.Add("SetTest", new Action<SovosExpando, string>((_this, value) =>
      {
        _this.Dictionary["_test"] = value;
      }));
      expando.Dictionary.Add("ResetTest", new Action<SovosExpando>(_this =>
      {
        _this.Dictionary["_test"] = "";
      }));
      return expando;
    }
  }
}