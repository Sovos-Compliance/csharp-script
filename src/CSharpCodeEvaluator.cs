using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
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
    private readonly CSharpCodeProvider codeProvider;
    private readonly CompilerParameters compilerParameters;
    private CompilerResults prg;
    private readonly List<Tuple<string, object>> objectsInScope;
    private readonly List<string> referencesNamespaces;
    private readonly string expression;
    private bool compiled;
    private object[] parameters;
    private MethodInfo methodInfo;
    private object holderObject;

    public CSharpExpression(string Expression)
    {
      codeProvider = new CSharpCodeProvider();
      compilerParameters = new CompilerParameters
      {
        CompilerOptions = "/t:library",
        GenerateInMemory = true
      };
      compilerParameters.ReferencedAssemblies.Add("system.dll");
      objectsInScope = new List<Tuple<string, object>>();
      referencesNamespaces = new List<string>();
      compiled = false;
      expression = Expression;
    }

    private void checkCompiled()
    {
      if (!compiled) return;

      compiled = false;
      methodInfo = null;
      holderObject = null;
      prg = null;
    }

    public void addReferencedAssembly(string assemblyName)
    {
      checkCompiled();
      compilerParameters.ReferencedAssemblies.Add(assemblyName);
    }

    public void addObjectInScope(string name, object obj)
    {
      checkCompiled();
      foreach (var _obj in objectsInScope)
      {
        if(string.Compare(_obj.Item1, 0, name, 0, name.Length, true) == 0)
          throw new ECSharpExpression($"Object in scope named '{name}' already exists");
      }
      objectsInScope.Add(new Tuple<string, object>(name, obj));
      var assemblyLocation = obj.GetType().Assembly.Location;
      if (!compilerParameters.ReferencedAssemblies.Contains(assemblyLocation))
        compilerParameters.ReferencedAssemblies.Add(assemblyLocation);
      var objectNamespace = obj.GetType().Namespace;
      if (!referencesNamespaces.Contains(objectNamespace))
        referencesNamespaces.Add(objectNamespace);
    }

    public void addUsedNamespace(string _namespace)
    {
      checkCompiled();
      referencesNamespaces.Add(_namespace);
    }

    private void compile()
    {
      checkCompiled();
      var sb = new StringBuilder("");
      sb.Append("using System;\n");
      foreach (var _namespace in referencesNamespaces)
      {
        sb.Append("using ");
        sb.Append(_namespace);
        sb.Append(";\n");
      }
      sb.Append("namespace CSCodeEvaler { \n");
      sb.Append("  public class CSCodeEvaler { \n");
      sb.Append("    public object EvalCode(");
      var firstParam = true;
      foreach (var objInScope in objectsInScope)
      {
        if(!firstParam) sb.Append(", ");
        sb.Append(objInScope.Item2.GetType().Name);
        sb.Append(" ");
        sb.Append(objInScope.Item1);
        firstParam = false;
      }
      sb.Append(")   { \n");
      sb.Append("    return ");
      sb.Append(expression);
      sb.Append("; \n");
      sb.Append("    } \n");
      sb.Append("  } \n");
      sb.Append("} \n");

      prg = codeProvider.CompileAssemblyFromSource(compilerParameters, sb.ToString());
      if (prg.Errors.Count > 0)
      {
        throw new InvalidExpressionException(prg.Errors[0].ErrorText);
      }
      parameters = new object[objectsInScope.Count];
      int index = 0;
      foreach (var param in objectsInScope)
      {
        parameters[index++] = param.Item2;
      }
      compiled = true;
    }

    public object execute()
    {
      if(!compiled) compile();

      if (holderObject == null)
      {
        var a = prg.CompiledAssembly;
        holderObject = a.CreateInstance("CSCodeEvaler.CSCodeEvaler");
        if (holderObject == null)
          throw new NullReferenceException("Host object in null");
      }
      if (methodInfo == null)
      {
        var t = holderObject.GetType();
        methodInfo = t.GetMethod("EvalCode");
        if(methodInfo == null)
          throw new NullReferenceException("methodInfo is null");
      }

      var returnObject = methodInfo.Invoke(holderObject, parameters);
      return returnObject;
    }
  }
}