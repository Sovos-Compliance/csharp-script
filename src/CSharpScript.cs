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
using System.Linq;
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
    private readonly IDictionary<string, uint> expressions;
    private readonly ICollection<string> members;
    private readonly CompilerParameters compilerParameters;
    private readonly IDictionary<string, object> objectsInScope;
    private readonly ICollection<string> usesNamespaces;

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
      AddReferencedAssembly(GetType().Assembly.Location);
      objectsInScope = new Dictionary<string, object>();
      usesNamespaces = new HashSet<string>();
      AddUsedNamespace("System");
      AddUsedNamespace("System.Dynamic");
      AddUsedNamespace("Sovos.Scripting.CSharpScriptObjectBase");
      AddUsedNamespace("System.Collections.Generic");
      expressions = new Dictionary<string, uint>();
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

    public void DisposeClr()
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
      lock (this)
      {
        if (state == State.NotCompiled) return;

        Invalidate();
      }
    }

    private uint AddCode(string Expression)
    {
      InvalidateIfCompiled();
      lock (this)
      {
        uint expressionId;
        if (expressions.TryGetValue(Expression, out expressionId))
          return expressionId;
        expressions.Add(Expression, expressionCount);
        return expressionCount++;
      }
    }

    private void SetHostOjectField(ICSharpScriptObjectAccessor scriptObject, string fieldName, object obj)
    {
      if (scriptObject == null) return;
      while (true)
      {
        try
        {
          if(!executeInSeparateAppDomain)
            scriptObject.SetField(fieldName, obj);
          else
            scriptObject.SetField(fieldName, ObjectAddress.GetAddress(obj));
          break;
        }
        catch (NotSupportedException)
        {
          // capture the exception and try again. This happens because there was a GC call between
          // the time we obtained the raw address of obj and the call to SetField()
        }
      }
    }

    private void SetObjectsInScope(ICSharpScriptObjectAccessor scriptObject)
    {
      lock (this)
        foreach (var obj in objectsInScope)
          SetHostOjectField(scriptObject, obj.Key, obj.Value);
    }

    private object BuildObject()
    {
      if (!executeInSeparateAppDomain)
        return prg.CompiledAssembly.CreateInstance("Sovos.CodeEvaler.CodeEvaler");
      lock (this)
      {
        // ToDo: Do we need lock for this?
        return appDomain.CreateInstanceFromAndUnwrap(prg.PathToAssembly, "Sovos.CodeEvaler.CodeEvaler");
      }
    }

    private void PrepareSeparateAppDomainIfNeeded()
    {
      if (!executeInSeparateAppDomain) return;
      if (appDomain != null) return;
      lock (this)
      {
        if (appDomain != null) return;

        var appDomainSetup = new AppDomainSetup()
        {
          ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
          LoaderOptimization = LoaderOptimization.MultiDomainHost
        };
        appDomain = AppDomain.CreateDomain("CSharpExpression_AppDomain" + GetHashCode(),
          AppDomain.CurrentDomain.Evidence, appDomainSetup);
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
      return AddCode("return " + Expression.Trim());
    }

    public uint AddVoidReturnCodeSnippet(string Expression)
    {
      return AddCode(Expression.Trim() + ";return null");
    }

    public uint AddCodeSnippet(string Expression)
    {
      return AddCode(Expression.Trim());
    }
    
    public void AddMember(string body)
    {
      InvalidateIfCompiled();
      lock (this)
        members.Add(body);
    }

    public void AddReferencedAssembly(string assemblyName)
    {
      InvalidateIfCompiled();
      assemblyName = assemblyName.ToUpper();
      lock (this)
        if (!compilerParameters.ReferencedAssemblies.Contains(assemblyName))
          compilerParameters.ReferencedAssemblies.Add(assemblyName);
    }

    public void AddObjectInScope(string name, object obj)
    {
      InvalidateIfCompiled();
      lock (this)
      {
        if (objectsInScope.ContainsKey(name))
          throw new CSharpScriptException(string.Format("Object in scope named '{0}' already exists", name));
        objectsInScope.Add(name, obj);
        AddReferencedAssembly(Path.GetFileName(obj.GetType().Assembly.Location));
        AddUsedNamespace(obj.GetType().Namespace);
      }
    }

    public void ReplaceObjectInScope(object scriptObject, string name, object obj)
    {
      SetHostOjectField((ICSharpScriptObjectAccessor) scriptObject, name, obj);
    }

    public void ReplaceObjectInScope(string name, object obj)
    {
      lock (this)
      {
        if (!objectsInScope.ContainsKey(name))
          throw new CSharpScriptException(string.Format("Object in scope named '{0}' not found", name));
        objectsInScope[name] = obj;
      }
      ReplaceObjectInScope(holderObjectAccesor, name, obj);
    }

    public void AddUsedNamespace(string _namespace)
    {
      InvalidateIfCompiled();
      lock (this)
        if (!usesNamespaces.Contains(_namespace))
          usesNamespaces.Add(_namespace);
    }

    public void GenerateCode()
    {
      if (state >= State.CodeGenerated) return;
      lock (this)
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
        var expressionsSortedIterator = expressions
          .OrderBy(kvp => kvp.Value)
          .Select(kvp => kvp.Key);
        foreach (var expr in expressionsSortedIterator)
        {
          sb += "case ";
          sb += i++.ToString();
          sb += ": ";
          sb += "\r\n";
          sb += expr;
          sb += ";\r\n";
        }
        sb += "default: throw new Exception(\"Invalid exprNo parameter\");\r\n};\r\n}\r\n}\r\n}";
        programText = sb.ToString();
        state = State.CodeGenerated;
      }
    }

    public void Compile()
    {
      if (state >= State.Compiled) return;
      GenerateCode();
      lock (this)
      {
        if (state >= State.Compiled) return;

        using (var codeProvider = new CSharpCodeProvider())
        {
          compilerParameters.OutputAssembly = "";
          Directory.CreateDirectory(TempLocation);
          compilerParameters.TempFiles = new TempFileCollection(TempLocation, false);
          prg = codeProvider.CompileAssemblyFromSource(compilerParameters, ProgramText);
        }
        if (prg.Errors.Count > 0)
        {
          var lines = ProgramText.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
          throw new InvalidExpressionException(string.Format("{0} in expression \"{1}\"",
            prg.Errors[0].ErrorText,
            prg.Errors[0].Line > 0 ? lines[prg.Errors[0].Line - 1] : "<source code not found>"));
        }
        state = State.Compiled;
      }
    }
    
    public void Prepare(bool buildDefaultObject = true)
    {
      if (state == State.Prepared) return;
      Compile();
      PrepareSeparateAppDomainIfNeeded();
      lock (this)
      {
        if (state == State.Prepared) return;

        if (buildDefaultObject)
        {
          holderObjectAccesor = (ICSharpScriptObjectAccessor) BuildObject();
          if (holderObjectAccesor == null)
            throw new NullReferenceException("Default host object is null");
          SetObjectsInScope(holderObjectAccesor);
        }
        state = State.Prepared;
      }
    }

    public object CreateScriptObject()
    {
      Prepare(false);
      var obj = BuildObject();
      SetObjectsInScope((ICSharpScriptObjectAccessor) obj);
      return obj;
    }

    public void Unprepare()
    {
      lock(this)
        Invalidate();
    }
    
    public object Execute(object hostObject, uint exprNo = 0)
    {
      Prepare();
      return ((ICSharpScriptObjectAccessor)hostObject).Eval(exprNo);
    }

    public object Execute(uint exprNo = 0)
    {
      Prepare();
      lock (this) 
        return Execute(holderObjectAccesor, exprNo);
    }

    public object Invoke(object hostObject, string methodName, object[] args)
    {
      Prepare();
      return ((ICSharpScriptObjectAccessor)hostObject).Invoke(methodName, args);
    }

    public object Invoke(string methodName, object[] args)
    {
      Prepare();
      lock (this)
        return Invoke(holderObjectAccesor, methodName, args);
    }

    public void UnloadAppDomain()
    {
      lock(this)
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
        lock (this)
        {
          if (value == executeInSeparateAppDomain) return;

          executeInSeparateAppDomain = value;
          compilerParameters.GenerateInMemory = !executeInSeparateAppDomain;
        }
      }
    }
    #endregion
  }
}