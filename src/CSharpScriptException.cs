using System;

namespace Sovos.Scripting.CSharpScriptObjectBase
{
  public class CSharpScriptException : Exception, ICSharpScriptObjectMethodInvoker
  {
    private string _message = "";

    public CSharpScriptException(){}
    public CSharpScriptException(string msg) : base(msg){}

    public object Invoke(string methodName, object[] args)
    {
      var method = GetType().GetMethod(methodName);
      if (method != null)
        return method.Invoke(this, args);
      throw new MissingMethodException(string.Format("Method \"{0}\"not found", methodName));
    }

    public override string Message
    {
      get
      {
        return _message != "" ? _message : base.Message;
      }
    }

    public void SetMessage(string message)
    {
      _message = message;
    }
  }
}