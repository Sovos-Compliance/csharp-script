/*
  Very important note on the usage of this module: PLEASE READ IF YOU PLAN TO RUN YOUR SCRIPTS IN DIFFERENT AppDomain!!

  First, why using different AppDomains (set property ExecuteInSeparateAppDomain to true)? 
  You want to do this, if you plan to:
     Run "scripted" C# code that changes "a lot" during the lifetime of a process. If you don't use different AppDomains 
     you can't unload assemblies you may not need anymore

  In order to run scripts that access shared object from different AppDomain, you need to:

  1. Put all your shared objects in an assembly, and install the assembly in the GAC (Assembly must be signed for this!)
  2. Use [LoaderOptimization(LoaderOptimization.MultiDomainHost] on your root program. If you don't do so, unloading of 
     AppDomain (and therefore of temporary assemblies) will simply fail and you will clutter your app with assemblies 
     you are not intending to keep loaded
  3. If you run your app from the VS debugger, go to Properties in your project, select Debug tab and make sure 
     "Enable the Visual Studio hosting process" is unchecked
 */

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Text;
using Microsoft.CSharp;
using Sovos.Infrastructure;

namespace Sovos.CSharpCodeEvaluator
{
  public class CSharpExpressionException : Exception
  {
    public CSharpExpressionException(string msg) : base(msg) {}
  }
  
  public class CSharpExpression : IDisposable
  {
    #region Private CSharpExpression types
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
    private readonly CompilerParameters compilerParameters;
    private readonly Dictionary<string, object> objectsInScope;
    private readonly List<string> usesNamespaces;

    // These fields will be != null when there's a valid compiled and prepared expressions
    private CompilerResults prg;
    private ICSharpExpressionAccessor holderObjectAccesor;
    private string programText;
    private AppDomain appDomain;
    private bool executeInSeparateAppDomain;
    #endregion

    #region Constructors and Desturctor
    public CSharpExpression(string Expression = "")
    {
      compilerParameters = new CompilerParameters
      {
        CompilerOptions = "/t:library /optimize",
        GenerateInMemory = true
      };
      AddReferencedAssembly("SYSTEM.DLL");
      AddReferencedAssembly("SYSTEM.CORE.DLL");
      AddReferencedAssembly("MICROSOFT.CSHARP.DLL");
      AddReferencedAssembly(Path.GetFileName(GetType().Assembly.Location));
      objectsInScope = new Dictionary<string, object>();
      usesNamespaces = new List<string> { "System", "System.Dynamic", "Sovos.Infrastructure", "System.Collections.Generic" };
      expressions = new List<string>();
      functions = new List<string>();
      if (Expression != "")
        AddExpression(Expression);
      state = State.NotCompiled;
    }

    ~CSharpExpression()
    {
      Dispose(false);
    }

    public void Dispose()
    {
      Dispose(true);
    }

    public void Dispose(bool disposing)
    {
      if (!disposing) return;
      TryUnloadAppDomain();
      TryRemoveTemporaryAssembly();
    }
    #endregion

    #region Private Methods
    private void TryUnloadAppDomain()
    {
      if (executeInSeparateAppDomain && appDomain != null)
        AppDomain.Unload(appDomain);
    }

    private void TryRemoveTemporaryAssembly()
    {
      if (prg == null || !File.Exists(prg.PathToAssembly)) return;
      try
      {
        File.Delete(prg.PathToAssembly);
      }
      catch (Exception)
      {
        // ignore any exception trying to remove assembly. 
        // very likely the assembly is loaded in memory
      }
    }

    private void Invalidate()
    {
      holderObjectAccesor = null;
      prg = null;
      programText = "";
      TryUnloadAppDomain();
      TryRemoveTemporaryAssembly();
      appDomain = null;
      state = State.NotCompiled;
    }

    private void InvalidateIfCompiled()
    {
      if (state == State.NotCompiled) return;
      Invalidate();
    }

    private uint AddCode(string Expression)
    {
      InvalidateIfCompiled();
      expressions.Add(Expression);
      return expressionCount++;
    }

    private void SetHostOjectField(string fieldName, object obj)
    {
      while (true)
      {
        try
        {
          holderObjectAccesor.SetField(fieldName, ObjectAddress.GetAddress(obj));
          break;
        }
        catch (NotSupportedException)
        {
          // capture the exception and try again. This happens because there was a GC call between
          // the time we obtained the raw address of obj and the call to SetField()
        }
      }
    }
    #endregion

    #region Public Methods and properties
    public uint AddExpression(string Expression)
    {
      return AddCode("return " + Expression);
    }

    public uint AddVoidReturnCodeSnippet(string Expression)
    {
      return AddCode(Expression + ";return null");
    }

    public uint AddCodeSnippet(string Expression)
    {
      return AddCode(Expression + ";break");
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
      objectsInScope.Add(name, obj);
      AddReferencedAssembly(Path.GetFileName(obj.GetType().Assembly.Location));
      AddUsedNamespace(obj.GetType().Namespace);
    }

    public void ReplaceObjectInScope(string name, object obj)
    {
      if (!objectsInScope.ContainsKey(name))
        throw new CSharpExpressionException(String.Format("Object in scope named '{0}' not found", name));
      objectsInScope[name] = obj;
      SetHostOjectField(name, obj);
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
      sb.Append("public class CodeEvaler:CSharpExpressionBase{");
      sb.Append("private dynamic global;");
      foreach (var fn in functions)
        sb.Append(fn);
      sb.Append("public CodeEvaler(){");
      sb.Append("global=new ExpandoObject();}");
      foreach (var objInScope in objectsInScope)
      {
        sb.Append("public ");
        sb.Append(objInScope.Value is ExpandoObject || objInScope.Value is DynamicObject ? "dynamic" : objInScope.Value.GetType().Name);
        sb.Append(" ");
        sb.Append(objInScope.Key);
        sb.Append(";");
      }
      sb.Append("public override object Eval(uint exprNo){");
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
      using (var codeProvider = new CSharpCodeProvider())
      {
        compilerParameters.OutputAssembly = "";
        compilerParameters.TempFiles = new TempFileCollection(Path.GetTempPath(), false);
        prg = codeProvider.CompileAssemblyFromSource(compilerParameters, ProgramText);
      }
      if (prg.Errors.Count > 0)
        throw new InvalidExpressionException(prg.Errors[0].ErrorText);
      state = State.Compiled;
    }

    public void Prepare()
    {
      if (state == State.Prepared) return;
      Compile();
      if (executeInSeparateAppDomain)
      {
        var appDomainSetup = new AppDomainSetup()
        {
          ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
          LoaderOptimization = LoaderOptimization.MultiDomainHost
        };
        appDomain = AppDomain.CreateDomain("CSharpExpression_AppDomain" + GetHashCode(), AppDomain.CurrentDomain.Evidence, appDomainSetup);
        holderObjectAccesor = (ICSharpExpressionAccessor) appDomain.CreateInstanceFromAndUnwrap(prg.PathToAssembly, "Sovos.CodeEvaler.CodeEvaler");
      }
      else
        holderObjectAccesor = (ICSharpExpressionAccessor)prg.CompiledAssembly.CreateInstance("Sovos.CodeEvaler.CodeEvaler");
      if (holderObjectAccesor == null)
        throw new NullReferenceException("Host object is null");
      foreach (var obj in objectsInScope)
        SetHostOjectField(obj.Key, obj.Value);
      state = State.Prepared;
    }

    public void Unprepare()
    {
      Invalidate();
    }
    
    public object Execute(uint exprNo = 0)
    {
      Prepare();
      return holderObjectAccesor.Eval(exprNo);
    }

    public void UnloadAppDomain()
    {
      Invalidate();
    }

    public string ProgramText
    {
      get
      {
        GenerateCode();
        return programText;
      }
    }

    public bool ExecuteInSeparateAppDomain
    {
      get
      {
        return executeInSeparateAppDomain;
      }
      set
      {
        if (value == executeInSeparateAppDomain) return;
        InvalidateIfCompiled();
        executeInSeparateAppDomain = value;
        compilerParameters.GenerateInMemory = !executeInSeparateAppDomain;
      }
    }
    #endregion
  }
}