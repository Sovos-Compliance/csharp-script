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
    public ECSharpExpression(string msg) : base(msg) {}
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
    private delegate object RunExpressionDelegate(uint exprNo);

    private enum State
    {
      NotCompiled = 0,
      CodeGenerated = 1,
      Compiled = 2,
      Prepared = 3
    }

    #region Private Fields
    private State state;
    private uint expressionCount;
    private List<string> expressions;
    private List<string> functions; 
    private CSharpCodeProvider codeProvider;
    private CompilerParameters compilerParameters;
    private Dictionary<string, object> objectsInScope;
    private List<string> usesNamespaces;

    // These fields will be != null when there's a valid compiled and prepared expressions
    private RunExpressionDelegate runExpressionDelegate;
    private CompilerResults prg;
    private object holderObject;
    private string programText;
    #endregion
    
    #region Constructors
    public CSharpExpression(string Expression = "")
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
        CompilerOptions = "/t:library /optimize",
        GenerateInMemory = true
      };
      AddReferencedAssembly("SYSTEM.DLL");
      AddReferencedAssembly("SYSTEM.CORE.DLL");
      AddReferencedAssembly("MICROSOFT.CSHARP.DLL");
      objectsInScope = new Dictionary<string, object>();
      usesNamespaces = new List<string> { "System", "System.Dynamic" };
      state = State.NotCompiled;
      expressions = new List<string>();
      functions = new List<string>();
      if (Expression != "")
        AddExpression(Expression);
    }

    private void Invalidate()
    {
      holderObject = null;
      prg = null;
      runExpressionDelegate = null;
      programText = "";
      state = State.NotCompiled;
    }

    private void InvalidateIfCompiled()
    {
      if (state == State.NotCompiled) return;
      Invalidate();
    }
    #endregion

    #region Public Methods and properties
    public uint AddExpression(string Expression)
    {
      return AddCodeSnippet("return " + Expression);
    }

    public uint AddVoidReturnCodeSnippet(string Expression)
    {
      expressions.Add(Expression + ";return null");
      return expressionCount++;
    }

    public uint AddCodeSnippet(string Expression)
    {
      InvalidateIfCompiled();
      expressions.Add(Expression);
      return expressionCount++;
    }

    public void AddFunctionBody(string function)
    {
      InvalidateIfCompiled();
      functions.Add(function);
    }

    public void AddReferencedAssembly(string assemblyName)
    {
      InvalidateIfCompiled();
      assemblyName = assemblyName.ToUpper();
      if (!compilerParameters.ReferencedAssemblies.Contains(assemblyName))
        compilerParameters.ReferencedAssemblies.Add(assemblyName);
    }

    public void AddObjectInScope(string name, object obj)
    {
      InvalidateIfCompiled();
      if (objectsInScope.ContainsKey(name))
        throw new ECSharpExpression($"Object in scope named '{name}' already exists");
      objectsInScope.Add(name, obj);
      var assemblyLocation = Path.GetFileName(obj.GetType().Assembly.Location);
      AddReferencedAssembly(assemblyLocation);
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

    public void GenerateCode()
    {
      if (state >= State.CodeGenerated) return;
      var sb = new StringBuilder("");
      foreach (var _namespace in usesNamespaces)
      {
        sb.Append("using ");
        sb.Append(_namespace);
        sb.Append(";");
      }
      sb.Append("namespace Sovos.CodeEvaler{");
      sb.Append("public class CodeEvaler{");
      sb.Append("private dynamic global;");
      foreach (var fn in functions)
        sb.Append(fn);
      sb.Append("public CodeEvaler(){");
      sb.Append("global=new ExpandoObject();}");
      foreach (var objInScope in objectsInScope)
      {
        sb.Append("public ");
        sb.Append(objInScope.Value is ExpandoObject ? "dynamic" : objInScope.Value.GetType().Name);
        sb.Append(" ");
        sb.Append(objInScope.Key);
        sb.Append(";");
      }
      sb.Append("public object Eval(uint exprNo){");
      sb.Append("switch(exprNo){");
      var i = 0;
      foreach (var expr in expressions)
      {
        sb.Append("case ");
        sb.Append(i++);
        sb.Append(": ");
        sb.Append(expr);
        sb.Append(";");
      }
      sb.Append("default: throw new Exception(\"Invalid exprNo parameter\");};}}}");
      programText = sb.ToString();
      state = State.CodeGenerated;
    }

    public void Compile()
    {
      if (state >= State.Compiled) return;
      GenerateCode();
      prg = codeProvider.CompileAssemblyFromSource(compilerParameters, ProgramText);
      if (prg.Errors.Count > 0)
        throw new InvalidExpressionException(prg.Errors[0].ErrorText);
      state = State.Compiled;
    }

    public void Prepare()
    {
      if (state == State.Prepared) return;
      Compile();
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
      Invalidate();
    }
    
    public object Execute(uint exprNo = 0)
    {
      Prepare();
      return runExpressionDelegate(exprNo);
    }

    public string ProgramText
    {
      get
      {
        GenerateCode();
        return programText;
      }
    }
    #endregion
  }
}