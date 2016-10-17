using System;

namespace Sovos.Scripting.CSharpScriptObjectBase
{
  public interface ICSharpScriptException
  {
    void SetMessage(string message);
  }

  public class CSharpScriptException : Exception, ICSharpScriptException
  {
    private string _message = "";

    public CSharpScriptException(){}
    public CSharpScriptException(string msg) : base(msg){}

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