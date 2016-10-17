using System;
using System.Collections.Generic;
using Sovos.Infrastructure;

namespace Sovos.Scripting.CSharpScriptObjectBase
{
  public interface ICSharpScriptObjectFieldAccesor
  {
    void SetField(string fieldName, KeyValuePair<IntPtr, int> obj);
    void SetField(string fieldName, object obj);
  }

  public interface ICSharpScriptObjectAccessor : ICSharpScriptObjectFieldAccesor
  {
    object Eval(uint ExprNo);
    object Invoke(string methodName, object[] args);
  }

  public abstract class CSharpScriptObjectBase : MarshalByRefObject, ICSharpScriptObjectAccessor
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

    public virtual object Eval(uint ExprNo)
    {
      return null;
    }

    public object Invoke(string methodName, object[] args)
    {
      var method = GetType().GetMethod(methodName);
      if (method != null)
        return method.Invoke(this, args);
      throw new MissingMethodException(string.Format("Method \"{0}\"not found", methodName));
    }
  }
}