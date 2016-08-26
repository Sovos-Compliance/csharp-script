using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Text;
using Microsoft.CSharp;

namespace Sovos.CSharpCodeEvaluator
{
  // ReSharper disable once InconsistentNaming
  public class ECSharpExpression : Exception
  {
    public ECSharpExpression(string msg)
    {
    }
  }

  public class ObjectInScope
  {
    public string name { get; }
    public object obj { get; }
    public ObjectInScope(string name, object obj)
    {
      this.name = name;
      this.obj = obj;
    }
  }

  public class CSharpExpression
  {
    private delegate object RunExpressionDelegate();

    private enum State
    {
      NotCompiled = 0,
      Compiled = 1,
      Prepared = 2
    }

    #region Private Fields
    private State state;
    private string expression;
    private CSharpCodeProvider codeProvider;
    private CompilerParameters compilerParameters;
    private Dictionary<string, object> objectsInScope;
    private List<string> usesNamespaces;

    // These fields will be != null when there's a valid compiled and prepared expression
    private RunExpressionDelegate runExpressionDelegate;
    private CompilerResults prg;
    private object holderObject;
    #endregion

    #region Constructors
    public CSharpExpression(string Expression)
    {
      Init(Expression);
    }

    public CSharpExpression(string Expression, IEnumerable<ObjectInScope> objectsInScope)
    {
      Init(Expression);
      foreach (var obj in objectsInScope)
        AddObjectInScope(obj.name, obj.obj);
    }
    #endregion

    #region Private Methods
    private void Init(string Expression)
    {
      codeProvider = new CSharpCodeProvider();
      compilerParameters = new CompilerParameters
      {
        CompilerOptions = "/t:library",
        GenerateInMemory = true
      };
      compilerParameters.ReferencedAssemblies.Add("SYSTEM.DLL");
      objectsInScope = new Dictionary<string, object>();
      usesNamespaces = new List<string> { "System" };
      state = State.NotCompiled;
      expression = Expression;
    }

    private void InvalidateIfCompiled()
    {
      if (state == State.NotCompiled) return;
      holderObject = null;
      prg = null;
      runExpressionDelegate = null;
      state = State.NotCompiled;
    }
    #endregion

    #region Public Methods
    public void AddReferencedAssembly(string assemblyName)
    {
      InvalidateIfCompiled();
      if (!compilerParameters.ReferencedAssemblies.Contains(assemblyName))
        compilerParameters.ReferencedAssemblies.Add(assemblyName);
    }

    public void AddObjectInScope(string name, object obj)
    {
      InvalidateIfCompiled();
      if (objectsInScope.ContainsKey(name))
        throw new ECSharpExpression($"Object in scope named '{name}' already exists");
      objectsInScope.Add(name, obj);
      var assemblyLocation = Path.GetFileName(obj.GetType().Assembly.Location).ToUpper();
      AddReferencedAssembly(assemblyLocation);
      if (obj is ExpandoObject)
        AddReferencedAssembly("MICROSOFT.CSHARP.DLL");
      AddUsedNamespace(obj.GetType().Namespace);
    }

    public void ReplaceObjectInScope(string name, object obj)
    {
      if (!objectsInScope.ContainsKey(name))
        throw new ECSharpExpression($"Object in scope named '{name}' not found");
      objectsInScope[name] = obj;
      holderObject?.GetType().GetField(name).SetValue(holderObject, obj);
    }

    public void AddUsedNamespace(string _namespace)
    {
      InvalidateIfCompiled();
      if(!usesNamespaces.Contains(_namespace))
        usesNamespaces.Add(_namespace);
    }

    public void Compile()
    {
      if (state != State.NotCompiled) return;
      var sb = new StringBuilder("");
      foreach (var _namespace in usesNamespaces)
      {
        sb.Append("using ");
        sb.Append(_namespace);
        sb.Append(";\n");
      }
      sb.Append("namespace Sovos.CodeEvaler { \n");
      sb.Append("  public class CodeEvaler { \n");
      foreach (var objInScope in objectsInScope)
      {
        sb.Append("public ");
        sb.Append(objInScope.Value is ExpandoObject ? "dynamic" : objInScope.Value.GetType().Name);
        sb.Append(" ");
        sb.Append(objInScope.Key);
        sb.Append(";");
      }
      sb.Append("    public object Eval() { \n");
      sb.Append("    return ");
      sb.Append(expression);
      sb.Append("; \n");
      sb.Append("    } \n");
      sb.Append("  } \n");
      sb.Append("} \n");

      prg = codeProvider.CompileAssemblyFromSource(compilerParameters, sb.ToString());
      if (prg.Errors.Count > 0)
        throw new InvalidExpressionException(prg.Errors[0].ErrorText);

      state = State.Compiled;
    }

    public void Prepare()
    {
      switch (state)
      {
        case State.Prepared:
          return;
        case State.NotCompiled:
          Compile();
          break;
      }

      var a = prg.CompiledAssembly;
      holderObject = a.CreateInstance("Sovos.CodeEvaler.CodeEvaler");
      if (holderObject == null)
        throw new NullReferenceException("Host object in null");

      foreach (var obj in objectsInScope)
        holderObject.GetType().GetField(obj.Key).SetValue(holderObject, obj.Value);

      var t = holderObject.GetType();
      var methodInfo = t.GetMethod("Eval");
      if (methodInfo == null)
        throw new NullReferenceException("methodInfo is null");
      runExpressionDelegate = (RunExpressionDelegate) methodInfo.CreateDelegate(typeof (RunExpressionDelegate), holderObject);

      state = State.Prepared;
    }

    public void Unprepare()
    {
      InvalidateIfCompiled();
    }

    public object Execute()
    {
      Prepare();
      return runExpressionDelegate();
    }
    #endregion
  }
}