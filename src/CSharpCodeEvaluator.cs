using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Linq;
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

  public class CSharpExpression
  {
    private delegate object RunExpressionDelegate();

    private enum State
    {
      NotCompiled = 0,
      Compiled = 1,
      Prepared = 2
    }

    private readonly CSharpCodeProvider codeProvider;
    private readonly CompilerParameters compilerParameters;
    private CompilerResults prg;
    private readonly List<Tuple<string, object>> objectsInScope;
    private readonly List<string> usesNamespaces;
    private readonly string expression;
    private RunExpressionDelegate runExpressionDelegate;
    private object holderObject;
    private State state;

    public CSharpExpression(string Expression)
    {
      codeProvider = new CSharpCodeProvider();
      compilerParameters = new CompilerParameters
      {
        CompilerOptions = "/t:library",
        GenerateInMemory = true
      };
      compilerParameters.ReferencedAssemblies.Add("SYSTEM.DLL");
      compilerParameters.ReferencedAssemblies.Add("SYSTEM.CORE.DLL");
      compilerParameters.ReferencedAssemblies.Add("MICROSOFT.CSHARP.DLL");
      objectsInScope = new List<Tuple<string, object>>();
      usesNamespaces = new List<string> {"System", "System.Runtime.CompilerServices"};
      state = State.NotCompiled;
      expression = Expression;
    }

    private void InvalidateIfCompiled()
    {
      // ReSharper disable once RedundantCheckBeforeAssignment
      if (state == State.NotCompiled) return;
      state = State.NotCompiled;
    }

    public void AddReferencedAssembly(string assemblyName)
    {
      InvalidateIfCompiled();
      compilerParameters.ReferencedAssemblies.Add(assemblyName);
    }

    public void AddObjectInScope(string name, object obj)
    {
      InvalidateIfCompiled();
      if (objectsInScope.Any(_obj => string.Compare(_obj.Item1, 0, name, 0, name.Length, true) == 0))
        throw new ECSharpExpression($"Object in scope named '{name}' already exists");
      objectsInScope.Add(new Tuple<string, object>(name, obj));
      var assemblyLocation = Path.GetFileName(obj.GetType().Assembly.Location).ToUpper();
      if (!compilerParameters.ReferencedAssemblies.Contains(assemblyLocation))
        compilerParameters.ReferencedAssemblies.Add(assemblyLocation);
      var objectNamespace = obj.GetType().Namespace;
      if (!usesNamespaces.Contains(objectNamespace))
        usesNamespaces.Add(objectNamespace);
    }

    public void AddUsedNamespace(string _namespace)
    {
      InvalidateIfCompiled();
      usesNamespaces.Add(_namespace);
    }

    private void Compile()
    {
      InvalidateIfCompiled();
      var sb = new StringBuilder("");
      foreach (var _namespace in usesNamespaces)
      {
        sb.Append("using ");
        sb.Append(_namespace);
        sb.Append(";\n");
      }
      sb.Append("namespace CSCodeEvaler { \n");
      sb.Append("  public class CSCodeEvaler { \n");
      foreach (var objInScope in objectsInScope)
      {
        sb.Append("public ");
        sb.Append(objInScope.Item2 is ExpandoObject ? "dynamic" : objInScope.Item2.GetType().Name);
        sb.Append(" ");
        sb.Append(objInScope.Item1);
        sb.Append(";");
      }
      sb.Append("    public object EvalCode() { \n");
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
      if (state == State.NotCompiled) Compile();

      state = State.Compiled;
      var a = prg.CompiledAssembly;
      holderObject = a.CreateInstance("CSCodeEvaler.CSCodeEvaler");
      if (holderObject == null)
        throw new NullReferenceException("Host object in null");

      foreach (var obj in objectsInScope)
        holderObject.GetType().GetField(obj.Item1).SetValue(holderObject, obj.Item2);

      var t = holderObject.GetType();
      var methodInfo = t.GetMethod("EvalCode");
      if (methodInfo == null)
        throw new NullReferenceException("methodInfo is null");
      runExpressionDelegate =
        (RunExpressionDelegate) methodInfo.CreateDelegate(typeof (RunExpressionDelegate), holderObject);

      state = State.Prepared;
    }

    public object Execute()
    {
      if (state == State.NotCompiled) Compile();
      if (state == State.Compiled) Prepare();

      return runExpressionDelegate();
    }
  }
}