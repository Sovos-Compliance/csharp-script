using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;

namespace Sovos.CSharpCodeEvaluator
{
  public class CSharpExpressionException : Exception
  {
    public CSharpExpressionException(string msg) : base(msg) {}
  }
  
  public class CSharpExpression
  {
    #region Private CSharpExpression types
    private class ObjectFieldInfoPair
    {
      public object Object { get; set; }
      public FieldInfo fieldInfo { get; set; }
    }

    private delegate object RunExpressionDelegate(uint exprNo);

    private enum State
    {
      NotCompiled = 0,
      CodeGenerated = 1,
      Compiled = 2,
      Prepared = 3
    }
    #endregion

    #region Private Fields
    private State state;
    private uint expressionCount;
    private readonly List<string> expressions;
    private readonly List<string> functions; 
    private readonly CSharpCodeProvider codeProvider;
    private readonly CompilerParameters compilerParameters;
    private readonly Dictionary<string, ObjectFieldInfoPair> objectsInScope;
    private readonly List<string> usesNamespaces;

    // These fields will be != null when there's a valid compiled and prepared expressions
    private RunExpressionDelegate runExpressionDelegate;
    private CompilerResults prg;
    private object holderObject;
    private string programText;
    #endregion
    
    #region Constructors
    public CSharpExpression(string Expression = "")
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
      objectsInScope = new Dictionary<string, ObjectFieldInfoPair>();
      usesNamespaces = new List<string> { "System", "System.Dynamic" };
      expressions = new List<string>();
      functions = new List<string>();
      if (Expression != "")
        AddExpression(Expression);
      state = State.NotCompiled;
    }
    #endregion

    #region Private Methods
    private void Invalidate()
    {
      holderObject = null;
      prg = null;
      runExpressionDelegate = null;
      programText = "";
      foreach (var obj in objectsInScope)
        obj.Value.fieldInfo = null;
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
      return AddCodeSnippet(Expression + ";return null");
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
        throw new CSharpExpressionException(String.Format("Object in scope named '{0}' already exists", name));
      objectsInScope.Add(name, new ObjectFieldInfoPair{Object = obj, fieldInfo = null});
      AddReferencedAssembly(Path.GetFileName(obj.GetType().Assembly.Location));
      AddUsedNamespace(obj.GetType().Namespace);
    }

    public void ReplaceObjectInScope(string name, object obj)
    {
      if (!objectsInScope.ContainsKey(name))
        throw new CSharpExpressionException(String.Format("Object in scope named '{0}' not found", name));
      var objFldInfo = objectsInScope[name];
      objFldInfo.Object = obj;
      if(holderObject != null)
        objFldInfo.fieldInfo.SetValue(holderObject, obj);
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
        sb.Append(objInScope.Value.Object is ExpandoObject ? "dynamic" : objInScope.Value.Object.GetType().Name);
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
        throw new NullReferenceException("Host object is null");
      foreach (var obj in objectsInScope)
      {
        if (obj.Value.fieldInfo == null)
          obj.Value.fieldInfo = holderObject.GetType().GetField(obj.Key);
        obj.Value.fieldInfo.SetValue(holderObject, obj.Value.Object);
      }
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