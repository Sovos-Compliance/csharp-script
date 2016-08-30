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

  public class CustomExpando : DynamicObject
  {
    public IDictionary<string, object> Dictionary { get; set; }

    public CustomExpando()
    {
      Dictionary = new Dictionary<string, object>();
    }

    public int Count { get { return Dictionary.Keys.Count; } }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
      if (Dictionary.ContainsKey(binder.Name))
      {
        result = binder.Name + "=" + Dictionary[binder.Name];
        return true;
      }
      return base.TryGetMember(binder, out result); //means result = null and return = false
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
      if (!Dictionary.ContainsKey(binder.Name))
      {
        Dictionary.Add(binder.Name, value);
      }
      else
        Dictionary[binder.Name] = value;

      return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
    {
      if (Dictionary.ContainsKey(binder.Name) && Dictionary[binder.Name] is Delegate)
      {
        Delegate del = (Delegate)Dictionary[binder.Name];
        result = del.DynamicInvoke(args);
        return true;
      }
      return base.TryInvokeMember(binder, args, out result);
    }

    public override bool TryDeleteMember(DeleteMemberBinder binder)
    {
      if (Dictionary.ContainsKey(binder.Name))
      {
        Dictionary.Remove(binder.Name);
        return true;
      }

      return base.TryDeleteMember(binder);
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
      foreach (string name in Dictionary.Keys)
        yield return name;
    }
  }

  public class CSharpCodeEvaluatorTests
  {
    [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
    [Test]
    public void BasicExpression_Success()
    {
      using (var expression = new CSharpExpression("1 + 1"))
        Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void CodeSnippet_Success()
    {
      using (var expression = new CSharpExpression())
      {
        Assert.AreEqual(0, expression.AddCodeSnippet("var i = 1; return 1 + i"));
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void VoidReturningCodeSnippet_Success()
    {
      using (var expression = new CSharpExpression())
      {
        Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("var i = 1; Console.WriteLine(i)"));
        Assert.AreEqual(null, expression.Execute());
      }
    }

    [Test]
    public void TwoBasicExpressions_Success()
    {
      using (var expression1 = new CSharpExpression("1 + 1"))
      {
        using (var expression2 = new CSharpExpression("2 + 2"))
          Assert.AreEqual(6, (int)expression1.Execute() + (int)expression2.Execute());
      }
    }

    [Test]
    public void ExpressionWithOneParameter_Success()
    {
      using (var expression = new CSharpExpression("1 + a"))
      {
        expression.AddObjectInScope("a", 1);
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void ExpressionWithOneParameterForceRecompilation_Success()
    {
      using (var expression = new CSharpExpression("1 + a"))
      {
        expression.AddObjectInScope("a", 1);
        Assert.AreEqual(2, expression.Execute());
        expression.AddObjectInScope("b", 2);
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void ExpressionWithTwoParameters_Success()
    {
      using (var expression = new CSharpExpression("1 + a + c"))
      {
        expression.AddObjectInScope("a", 1);
        expression.AddObjectInScope("c", 3);
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    [ExpectedException("Sovos.CSharpCodeEvaluator.CSharpExpressionException")]
    public void ExpressionWithSameParameterTwice_Fails()
    {
      using (var expression = new CSharpExpression("1 + a + a"))
      {
        expression.AddObjectInScope("a", 1);
        expression.AddObjectInScope("a", 3);
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingLocalObject_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
      }
    }

    [Test]
    public void RunExpressionTwiceReferencingReplacedLocalObject_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
        var obj2 = new TestClass {int_Field = 3};
        expression.ReplaceObjectInScope("obj", obj2);
        Assert.AreEqual(4, expression.Execute());
      }
    }

    [Test]
    [ExpectedException("Sovos.CSharpCodeEvaluator.CSharpExpressionException")]
    public void ReplacedNonExistingLocalObject_Fails()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.ReplaceObjectInScope("obj", obj);
        Assert.AreEqual(4, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingTwoLocalObjects_Success()
    {
      using (var expression = new CSharpExpression("obj2.int_Field + obj1.int_Field"))
      {
        var obj1 = new TestClass {int_Field = 2};
        var obj2 = new TestClass {int_Field = 3};
        expression.AddObjectInScope("obj1", obj1);
        expression.AddObjectInScope("obj2", obj2);
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    public void ExecuteBasicExpressionTwice_Success()
    {
      using (var expression = new CSharpExpression("1 + 1"))
      {
        Assert.AreEqual(2, expression.Execute());
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingMutableLocalObject_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
        obj.int_Field = 5;
        Assert.AreEqual(6, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingExpandoObject_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        IDictionary<string, object> obj = new ExpandoObject();
        obj.Add("int_Field", 2);
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingMutableExpandoObject_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        IDictionary<string, object> obj = new ExpandoObject();
        obj.Add("int_Field", 2);
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
        dynamic dynObj = obj;
        dynObj.int_Field = 3;
        Assert.AreEqual(4, expression.Execute());
      }
    }

    [Test]
    [ExpectedException("System.Data.InvalidExpressionException")]
    public void ExpressionWithSyntaxError_Fails()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
        Assert.AreEqual(3, expression.Execute());
    }

    [Test]
    public void ShortPerformanceTest_Success()
    {
      using (var expression = new CSharpExpression("1 + obj.int_Field"))
      {
        var obj = new TestClass();
        expression.AddObjectInScope("obj", obj);
        var initialTicks = Environment.TickCount;
        for (obj.int_Field = 1; obj.int_Field < 1000000; obj.int_Field++)
          Assert.AreEqual(1 + obj.int_Field, expression.Execute());
        Assert.Less(Environment.TickCount - initialTicks, 1000); // 1MM iterations should run in less than 1 second
      }
    }

    [Test]
    public void ExpressionCountPressure_Success()
    {
      for (var i = 1; i < 50; i++)
      {
        using (var expression = new CSharpExpression(String.Format("{0} + 1", i)))
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
        for (var j = 0; j < tasks.Length - 1; j++)
          tasks[j].Dispose();
      }
    }

    [Test]
    public void StoreExpressionInDictionary_Success()
    {
      var Dict = new Dictionary<string, CSharpExpression>();
      using (var expr1 = new CSharpExpression("1 + 1"))
      {
        Dict.Add(expr1.ProgramText, expr1);
        CSharpExpression expr2;
        Assert.True(Dict.TryGetValue(expr1.ProgramText, out expr2));
        Assert.AreEqual(2, expr2.Execute());
      }
    }

    [Test]
    public void TwoBasicExpressionsInOneObject_Success()
    {
      using (var expression = new CSharpExpression())
      {
        Assert.AreEqual(0, expression.AddExpression("1 + 1"));
        Assert.AreEqual(1, expression.AddExpression("2 + 2"));
        Assert.AreEqual(2, expression.Execute());
        Assert.AreEqual(4, expression.Execute(1));
      }
    }

    [Test]
    [ExpectedException("System.Exception")]
    public void RunNonExistingExpressionNumber_Fails()
    {
      using (var expression = new CSharpExpression())
        expression.Execute(2);
    }

    [Test]
    public void OneThousendExpressionsInOneObject_Success()
    {
      using (var expression = new CSharpExpression())
      {
        for (uint i = 1; i < 1000; i++)
          Assert.AreEqual(i - 1, expression.AddExpression(String.Format("{0} + 1", i)));
        for (uint i = 1; i < 1000; i++)
          Assert.AreEqual(i + 1, expression.Execute(i - 1));
      }
    }

    [Test]
    public void ExpressionUsingGlobalToKeepStateAcrossExecutions_Success()
    {
      using (var expression = new CSharpExpression())
      {
        Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("global.a = 0"));
        Assert.AreEqual(1, expression.AddExpression("global.a++"));
        Assert.AreEqual(null, expression.Execute()); // setup the global, runs first expression
        Assert.AreEqual(0, expression.Execute(1));
        Assert.AreEqual(1, expression.Execute(1));
      }
    }

    [Test]
    public void ExpressionUsingGlobalPerformance_Success()
    {
      using (var expression = new CSharpExpression())
      {
        Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("global.a = 0"));
        Assert.AreEqual(1, expression.AddExpression("global.a++"));
        Assert.AreEqual(null, expression.Execute()); // setup the global
        var initialTicks = Environment.TickCount;
        for (uint i = 0; i < 1000000; i++)
          Assert.AreEqual(i, expression.Execute(1));
        Assert.Less(Environment.TickCount - initialTicks, 2000); // 1 million executions in less than 2 seconds
      }
    }

    [Test]
    public void CallInjectedFunction_Success()
    {
      using (var expression = new CSharpExpression())
      {
        expression.AddFunctionBody(
          @"private int AddNumbers(int a, int b)
          {
            return a + b; 
          }");
        Assert.AreEqual(0, expression.AddExpression("AddNumbers(1, 2)"));
        Assert.AreEqual(3, expression.Execute());
      }
    }

    [Test]
    public void BasicExpressionSeparateAppDomain_Success()
    {
      using (var expression = new CSharpExpression("1 + 1"))
      {
        expression.ExecuteInSeparateAppDomain = true;
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void BasicExpressionSeparateAppDomainShareIntegerObject_Success()
    {
      using (var expression = new CSharpExpression("1 + a"))
      {
        expression.ExecuteInSeparateAppDomain = true;
        expression.AddObjectInScope("a", 1);
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void UseExpandoObjectWithDynamicMethod_Success()
    {
      using (var expression = new CSharpExpression("\"a\" + a.Test(\"a\")"))
      {
        dynamic expando = new ExpandoObject();
        expando.Test = new Func<string, string>(str => "Hello" + str);
        expression.AddObjectInScope("a", expando);
        Assert.AreEqual("aHelloa", expression.Execute());
      }
    }
    
    [Test]
    public void UseCustomExpandoObjectWithProperty_Success()
    {
      using (var expression = new CSharpExpression("a.Test"))
      {
        dynamic expando = new CustomExpando();
        expando.Test = "Hi";
        expression.AddObjectInScope("a", expando);
        Assert.AreEqual("Test=Hi", expression.Execute());
      }
    }
  }
}
