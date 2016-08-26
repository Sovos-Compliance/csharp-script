using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using NUnit.Framework;
using Sovos.CSharpCodeEvaluator;

namespace csharp_code_evaluator_ut
{
  public class TestClass
  {
    public int int_Field;
  }

  public class CSharpCodeEvaluatorTests
  {
    [Test]
    public void BasicExpression_Success()
    {
      var expression = new CSharpExpression("1 + 1");
      Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void CodeSnippet_Success()
    {
      var expression = new CSharpExpression();
      Assert.AreEqual(0, expression.AddCodeSnippet("var i = 1; return 1 + i"));
      Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void VoidReturningCodeSnippet_Success()
    {
      var expression = new CSharpExpression();
      Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("var i = 1; Console.WriteLine(i)"));
      Assert.AreEqual(null, expression.Execute());
    }

    [Test]
    public void TwoBasicExpressions_Success()
    {
      var expression1 = new CSharpExpression("1 + 1");
      var expression2 = new CSharpExpression("2 + 2");
      Assert.AreEqual(6, (int)expression1.Execute() + (int)expression2.Execute());
    }

    [Test]
    public void ExpressionWithOneParameter_Success()
    {
      var expression = new CSharpExpression("1 + a");
      expression.AddObjectInScope("a", 1);
      Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void ExpressionWithOneParameterForceRecompilation_Success()
    {
      var expression = new CSharpExpression("1 + a");
      expression.AddObjectInScope("a", 1);
      Assert.AreEqual(2, expression.Execute());
      expression.AddObjectInScope("b", 2);
      Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void ExpressionWithTwoParameters_Success()
    {
      var expression = new CSharpExpression("1 + a + c");
      expression.AddObjectInScope("a", 1);
      expression.AddObjectInScope("c", 3);
      Assert.AreEqual(5, expression.Execute());
    }

    [Test]
    public void ExpressionWithTwoParametersUsingOverloadConstructor_Success()
    {
      var expression = new CSharpExpression("1 + a + c", new[]{ new ObjectInScope("a", 1), new ObjectInScope("c", 3) });
      Assert.AreEqual(5, expression.Execute());
    }

    [Test]
    [ExpectedException("Sovos.CSharpCodeEvaluator.ECSharpExpression")]
    public void ExpressionWithSameParameterTwice_Fails()
    {
      var expression = new CSharpExpression("1 + a + a");
      expression.AddObjectInScope("a", 1);
      expression.AddObjectInScope("a", 3);
      Assert.AreEqual(5, expression.Execute());
    }

    [Test]
    public void ExpressionReferencingLocalObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass {int_Field = 2};
      expression.AddObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.Execute());
    }

    [Test]
    public void RunExpressionTwiceReferencingReplacedLocalObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass { int_Field = 2 };
      expression.AddObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.Execute());
      var obj2 = new TestClass { int_Field = 3 };
      expression.ReplaceObjectInScope("obj", obj2);
      Assert.AreEqual(4, expression.Execute());
    }

    [Test]
    [ExpectedException("Sovos.CSharpCodeEvaluator.ECSharpExpression")]
    public void ReplacedNonExistingLocalObject_Fails()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass { int_Field = 2 };
      expression.ReplaceObjectInScope("obj", obj);
      Assert.AreEqual(4, expression.Execute());
    }

    [Test]
    public void ExpressionReferencingTwoLocalObjects_Success()
    {
      var expression = new CSharpExpression("obj2.int_Field + obj1.int_Field");
      var obj1 = new TestClass { int_Field = 2 };
      var obj2 = new TestClass { int_Field = 3 };
      expression.AddObjectInScope("obj1", obj1);
      expression.AddObjectInScope("obj2", obj2);
      Assert.AreEqual(5, expression.Execute());
    }

    [Test]
    public void ExecuteBasicExpressionTwice_Success()
    {
      var expression = new CSharpExpression("1 + 1");
      Assert.AreEqual(2, expression.Execute());
      Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void ExpressionReferencingMutableLocalObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass { int_Field = 2 };
      expression.AddObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.Execute());
      obj.int_Field = 5;
      Assert.AreEqual(6, expression.Execute());
    }

    [Test]
    public void ExpressionReferencingExpandoObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      IDictionary<string, object> obj = new ExpandoObject();
      obj.Add("int_Field", 2);
      expression.AddObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.Execute());
    }

    [Test]
    public void ExpressionReferencingMutableExpandoObject_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      IDictionary<string, object> obj = new ExpandoObject();
      obj.Add("int_Field", 2);
      expression.AddObjectInScope("obj", obj);
      Assert.AreEqual(3, expression.Execute());
      dynamic dynObj = obj;
      dynObj.int_Field = 3;
      Assert.AreEqual(4, expression.Execute());
    }

    [Test]
    [ExpectedException("System.Data.InvalidExpressionException")]
    public void ExpressionWithSyntaxError_Fails()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      Assert.AreEqual(3, expression.Execute());
    }

    [Test]
    public void ShortPerformanceTest_Success()
    {
      var expression = new CSharpExpression("1 + obj.int_Field");
      var obj = new TestClass();
      expression.AddObjectInScope("obj", obj);
      var initialTicks = Environment.TickCount;
      for (obj.int_Field = 1; obj.int_Field < 1000000; obj.int_Field++)
        Assert.AreEqual(1 + obj.int_Field, expression.Execute());
      Assert.Less(Environment.TickCount - initialTicks, 1000); // 1MM iterations should run in less than 1 second
    }

    [Test]
    public void ExpressionCountPressure_Success()
    {
      for (var i = 1; i < 50; i++)
      {
        var expression = new CSharpExpression($"{i} + 1");
        Assert.AreEqual(1 + i, expression.Execute());
      }
    }

    private static CSharpExpression Compile(string _expression)
    {
      var expression = new CSharpExpression(_expression);
      expression.Prepare();
      return expression;
    }

    [Test]
    public async void ExpressionParalellCompilation_Success()
    {
      var tasks = new Task[4]; // We will compile expressions in 4 threads
      for (var i = 1; i < 25; i++)
      {
        for (var j = 0; j < tasks.Length - 1; j++)
          tasks[j] = Task.Run(() => Compile("1 + 1"));
        for (var j = 0; j < tasks.Length - 1; j++)
        {
          var expression = await (Task<CSharpExpression>)tasks[j];
          Assert.AreEqual(2, expression.Execute());
        }
      }
    }

    [Test]
    public void StoreExpressionInDictionary_Success()
    {
      var Dict = new Dictionary<string, CSharpExpression>();
      var expr1 = new CSharpExpression("1 + 1");
      Dict.Add(expr1.ProgramText, expr1);
      CSharpExpression expr2;
      Assert.True(Dict.TryGetValue(expr1.ProgramText, out expr2));
      Assert.AreEqual(2, expr2.Execute());
    }

    [Test]
    public void TwoBasicExpressionsInOneObject_Success()
    {
      var expression = new CSharpExpression();
      Assert.AreEqual(0, expression.AddExpression("1 + 1"));
      Assert.AreEqual(1, expression.AddExpression("2 + 2"));
      Assert.AreEqual(2, expression.Execute());
      Assert.AreEqual(4, expression.Execute(1));
    }

    [Test]
    [ExpectedException("System.Exception")]
    public void RunNonExistingExpressionNumber_Fails()
    {
      var expression = new CSharpExpression();
      expression.Execute(2);
    }

    [Test]
    public void OneThousendExpressionsInOneObject_Success()
    {
      var expression = new CSharpExpression();
      for(var i = 1; i < 1000; i++)
        Assert.AreEqual(i - 1, expression.AddExpression($"{i} + 1"));
      for (var i = 1; i < 1000; i++)
        Assert.AreEqual(i + 1, expression.Execute(i - 1));
    }

    [Test]
    public void ExpressionUsingGlobalToKeepStateAcrossExecutions_Success()
    {
      var expression = new CSharpExpression();
      Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("global.a = 0"));
      Assert.AreEqual(1, expression.AddExpression("global.a++"));
      Assert.AreEqual(null, expression.Execute()); // setup the global, runs first expression
      Assert.AreEqual(0, expression.Execute(1));
      Assert.AreEqual(1, expression.Execute(1));
    }

    [Test]
    public void ExpressionUsingGlobalPerformance_Success()
    {
      var expression = new CSharpExpression();
      Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("global.a = 0"));
      Assert.AreEqual(1, expression.AddExpression("global.a++"));
      Assert.AreEqual(null, expression.Execute()); // setup the global
      var initialTicks = Environment.TickCount;
      for (var i = 0; i < 1000000; i++) 
        Assert.AreEqual(i, expression.Execute(1));
      Assert.Less(Environment.TickCount - initialTicks, 2000); // 1 million executions in less than 2 seconds
    }
  }
}
