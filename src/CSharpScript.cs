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
using Sovos.Scripting.CSharpScriptObjectBase;

namespace Sovos.Scripting
{
  public class CSharpScriptException : Exception
  {
    public CSharpScriptException(string msg) : base(msg) {}
  }
 
  public class CSharpScript : IDisposable
  {
    #region Private CSharpScript types
    private enum State
    {
      NotCompiled = 0,
      CodeGenerated = 1,
      Compiled = 2,
      Prepared = 3
    }

    private class CSharpScriptStringBuilder
    {
      private readonly StringBuilder sb;

      public CSharpScriptStringBuilder()
      {
        sb = new StringBuilder();
      }

      public override string ToString()
      {
        return sb.ToString();
      }

      public static CSharpScriptStringBuilder operator +(CSharpScriptStringBuilder sb, string s)
      {
        sb.sb.Append(s);
        return sb;
      }
    }
    #endregion

    #region Private Fields
    private State state;
    private uint expressionCount;
    private readonly List<string> expressions;
    private readonly List<string> members;
    private readonly CompilerParameters compilerParameters;
    private readonly Dictionary<string, object> objectsInScope;
    private readonly List<string> usesNamespaces;

    // These fields will be != null when there's a valid compiled and prepared expressions
    private CompilerResults prg;
    private ICSharpScriptObjectAccessor holderObjectAccesor;
    private string programText;
    private AppDomain appDomain;
    private bool executeInSeparateAppDomain;
    #endregion

    #region Constructors, Destructor and Disposal methods
    public CSharpScript(string Expression = "")
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
      usesNamespaces = new List<string> { "System", "System.Dynamic", "Sovos.Scripting.CSharpScriptObjectBase", "System.Collections.Generic" };
      expressions = new List<string>();
      members = new List<string>();
      if (Expression != "")
        AddExpression(Expression);
      state = State.NotCompiled;
    }

    ~CSharpScript()
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
      if (prg != null && File.Exists(prg.PathToAssembly))
        try
        {
          File.Delete(prg.PathToAssembly);
        }
        catch (Exception)
        {
          // ignore any exception trying to remove assembly. 
          // very likely the assembly is loaded in memory
        }
      try
      {
        var tmpFiles = Directory.EnumerateFiles(TempLocation);
        foreach (var file in tmpFiles)
          File.Delete(file);
        Directory.Delete(TempLocation);
      }
      catch (Exception)
      {
        // ignore any exception trying to remove temp directory or its files 
        // very likely there's still files being used
      }
    }

    private void Invalidate()
    {
      holderObjectAccesor = null;
      programText = "";
      TryUnloadAppDomain();
      TryRemoveTemporaryAssembly();
      prg = null;
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
          if(!executeInSeparateAppDomain)
            holderObjectAccesor.SetField(fieldName, obj);
          else
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

    private string tempLocation;
    private string TempLocation
    {
      get
      {
        if (string.IsNullOrEmpty(tempLocation))
          tempLocation = string.Format("{0}\\sovos_csharpexpression_{1}\\", Path.GetTempPath(), GetHashCode());
        return tempLocation;
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
    
    public void AddMember(string body)
    {
      InvalidateIfCompiled();
      members.Add(body);
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
        throw new CSharpScriptException(string.Format("Object in scope named '{0}' already exists", name));
      objectsInScope.Add(name, obj);
      AddReferencedAssembly(Path.GetFileName(obj.GetType().Assembly.Location));
      AddUsedNamespace(obj.GetType().Namespace);
    }

    public void ReplaceObjectInScope(string name, object obj)
    {
      if (!objectsInScope.ContainsKey(name))
        throw new CSharpScriptException(string.Format("Object in scope named '{0}' not found", name));
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
      var sb = new CSharpScriptStringBuilder();
      foreach (var _namespace in usesNamespaces)
      {
        sb += "using ";
        sb += _namespace;
        sb += ";\r\n";
      }
      sb += "namespace Sovos.CodeEvaler {\r\n";
      sb += "public class CodeEvaler : CSharpScriptObjectBase {\r\n";
      sb += "private dynamic global;\r\n";
      foreach (var body in members)
      {
        sb += body;
        sb += "\r\n";
      }
      sb += "public CodeEvaler() {\r\n";
      sb += "global=new ExpandoObject();\r\n}\r\n";
      foreach (var objInScope in objectsInScope)
      {
        sb += "public ";
        sb += objInScope.Value is IDynamicMetaObjectProvider ? "dynamic" : objInScope.Value.GetType().Name;
        sb += " ";
        sb += objInScope.Key;
        sb += ";\r\n";
      }
      sb += "public override object Eval(uint exprNo) {\r\n";
      sb += "switch(exprNo) {\r\n";
      var i = 0;
      foreach (var expr in expressions)
      {
        sb += "case ";
        sb += i++.ToString();
        sb += ": ";
        sb += expr;
        sb += ";\r\n";
      }
      sb += "default: throw new Exception(\"Invalid exprNo parameter\");\r\n};\r\n}\r\n}\r\n}";
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
        Directory.CreateDirectory(TempLocation);
        compilerParameters.TempFiles = new TempFileCollection(TempLocation, false);
        prg = codeProvider.CompileAssemblyFromSource(compilerParameters, ProgramText);
      }
      if (prg.Errors.Count > 0)
      {
        var lines = ProgramText.Split(new [] { Environment.NewLine }, StringSplitOptions.None);
        throw new InvalidExpressionException(string.Format("{0} in expression \"{1}\"", 
                                             prg.Errors[0].ErrorText, 
                                             prg.Errors[0].Line > 0 ? lines[prg.Errors[0].Line - 1] : "<source code not found>"));
      }
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
        holderObjectAccesor = (ICSharpScriptObjectAccessor) appDomain.CreateInstanceFromAndUnwrap(prg.PathToAssembly, "Sovos.CodeEvaler.CodeEvaler");
      }
      else
        holderObjectAccesor = (ICSharpScriptObjectAccessor)prg.CompiledAssembly.CreateInstance("Sovos.CodeEvaler.CodeEvaler");
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

    public object Invoke(string methodName, object[] args)
    {
      Prepare();
      return holderObjectAccesor.Invoke(methodName, args);
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