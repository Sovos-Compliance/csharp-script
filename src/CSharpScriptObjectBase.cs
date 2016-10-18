using System;
using System.Collections.Generic;
using System.Reflection;
using Sovos.Infrastructure;

namespace Sovos.Scripting.CSharpScriptObjectBase
{
  public interface ICSharpScriptObjectFieldAccesor
  {
    void SetField(string fieldName, KeyValuePair<IntPtr, int> obj);
    void SetField(string fieldName, object obj);
  }

  public interface ICSharpScriptObjectMethodInvoker
  {
    object Invoke(string methodName, object[] args);
  }

  public interface ICSharpScriptObjectExpressionEvaler
  {
    object Eval(uint ExprNo);
  }

  public class CSharpScriptObjectBase : MarshalByRefObject, 
    ICSharpScriptObjectExpressionEvaler, ICSharpScriptObjectMethodInvoker, ICSharpScriptObjectFieldAccesor
  {
    private static readonly PtrConverter<object> converter = new PtrConverter<object>();
    private readonly Dictionary<string, MethodInfo> _cachedMethodsInfos = new Dictionary<string, MethodInfo>(); 
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
      MethodInfo method;
      if (_cachedMethodsInfos.TryGetValue(methodName, out method))
        return method.Invoke(this, args);
      method = GetType().GetMethod(methodName);
      if(method == null)
        throw new MissingMethodException(string.Format("Method \"{0}\"not found", methodName));
      _cachedMethodsInfos.Add(methodName, method);
      return method.Invoke(this, args);
    }
  }
}