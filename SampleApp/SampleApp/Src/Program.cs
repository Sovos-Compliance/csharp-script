using System;
using CSharpCodeEvaluatorTestClass;
using Sovos.CSharpCodeEvaluator;

namespace SampleApp
{
  class Program
  {
    [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
    static void Main(string[] args)
    {
      CSharpExpression expression = new CSharpExpression("1 + a.a") {ExecuteInSeparateAppDomain = true};
      // This snippet shows how a class on a assembly in the GAC can be used
      using (expression)
      {
        expression.AddObjectInScope("a", new TestClassInGACAssembly());
        Console.WriteLine(expression.Execute());
      }

      // This snippet shows how building a SovosExpando, in an assembly installed in GAC works fine.
      // You can call all methods dynamically from the C# script
      using (expression = new CSharpExpression() { ExecuteInSeparateAppDomain = true })
      {
        var expando = SovosExpandoBuilder.Build();
        expression.AddObjectInScope("sovosExpando", expando);
        expression.AddCodeSnippet(
          @"var v = sovosExpando.Test; // We read here a property that gets created on-the-fly
            sovosExpando.Test = ""Hola Mundo""; // We enter a value here that will be cleared by ResetTest() call
            sovosExpando.ResetTest(); // We invoke a dynamically created method here
            sovosExpando.Test = v + sovosExpando.Test + ""Hello World""; // We use here the property read on-the-fly and stored in v
            return sovosExpando.Test");
        Console.WriteLine(expression.Execute());
      }
    }
  }
}
