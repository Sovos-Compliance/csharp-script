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
      expression.AddObjectInScope("a", new TestClassInGACAssembly());
      Console.WriteLine(expression.Execute());
    }
  }
}
