using System;
using System.Collections.Generic;

namespace Sovos.Infrastructure
{
  public interface ICSharpExpressionAccessor
  {
    void SetField(string fieldName, KeyValuePair<IntPtr, int> obj);
    void SetField(string fieldName, object obj);
    object Eval(uint ExprNo);
  }

  public abstract class CSharpExpressionBase : MarshalByRefObject, ICSharpExpressionAccessor
  {
    private static readonly PtrConverter<object> converter = new PtrConverter<object>();
    public void SetField(string fieldName, KeyValuePair<IntPtr, int> obj)
    {
      if (ObjectAddress.GCCount != obj.Value)
        throw new NotSupportedException("GCCount changed since IntPtr of object was obtained. Please try again");
      GetType().GetField(fieldName).SetValue(this, converter.ConvertFromIntPtr(obj.Key));
    }

    public void SetField(string fieldName, object obj)
    {
      GetType().GetField(fieldName).SetValue(this, obj);
    }

    public abstract object Eval(uint ExprNo);
  }
}