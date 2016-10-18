using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;
using NUnit.Framework;
using Sovos.Scripting;
using SampleApp;
using Sovos.Scripting.CSharpScriptObjectBase;

namespace csharp_code_evaluator_ut
{
  public class TestClass
  {
    public int int_Field;
  }
  
  public class CSharpCodeEvaluatorTests
  {
    [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
    [Test]
    public void BasicExpression_Success()
    {
      using (var expression = new CSharpScript("1 + 1"))
        Assert.AreEqual(2, expression.Execute());
    }

    [Test]
    public void CodeSnippet_Success()
    {
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddCodeSnippet("var i = 1; return 1 + i"));
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void VoidReturningCodeSnippet_Success()
    {
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("var i = 1; Console.WriteLine(i)"));
        Assert.AreEqual(null, expression.Execute());
      }
    }

    [Test]
    public void TwoBasicExpressions_Success()
    {
      using (var expression1 = new CSharpScript("1 + 1"))
      {
        using (var expression2 = new CSharpScript("2 + 2"))
          Assert.AreEqual(6, (int)expression1.Execute() + (int)expression2.Execute());
      }
    }

    [Test]
    public void ExpressionWithOneParameter_Success()
    {
      using (var expression = new CSharpScript("1 + a"))
      {
        expression.AddObjectInScope("a", 1);
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void ExpressionWithOneParameterForceRecompilation_Success()
    {
      using (var expression = new CSharpScript("1 + a"))
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
      using (var expression = new CSharpScript("1 + a + c"))
      {
        expression.AddObjectInScope("a", 1);
        expression.AddObjectInScope("c", 3);
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    [ExpectedException("Sovos.Scripting.CSharpScriptObjectBase.CSharpScriptException")]
    public void ExpressionWithSameParameterTwice_Failure()
    {
      using (var expression = new CSharpScript("1 + a + a"))
      {
        expression.AddObjectInScope("a", 1);
        expression.AddObjectInScope("a", 3);
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingLocalObject_Success()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.AddObjectInScope("obj", obj);
        Assert.AreEqual(3, expression.Execute());
      }
    }

    [Test]
    public void RunExpressionTwiceReferencingReplacedLocalObject_Success()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
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
    [ExpectedException("Sovos.Scripting.CSharpScriptObjectBase.CSharpScriptException")]
    public void ReplacedNonExistingLocalObject_Failure()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
      {
        var obj = new TestClass {int_Field = 2};
        expression.ReplaceObjectInScope("obj", obj);
        Assert.AreEqual(4, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingTwoLocalObjects_Success()
    {
      using (var expression = new CSharpScript("obj2.int_Field + obj1.int_Field"))
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
      using (var expression = new CSharpScript("1 + 1"))
      {
        Assert.AreEqual(2, expression.Execute());
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void ExpressionReferencingMutableLocalObject_Success()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
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
      using (var expression = new CSharpScript("1 + obj.int_Field"))
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
      using (var expression = new CSharpScript("1 + obj.int_Field"))
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
    public void ExpressionWithSyntaxError_Failure()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
        expression.Compile();
    }

    [Test]
    public void ShortPerformanceTest_Success()
    {
      using (var expression = new CSharpScript("1 + obj.int_Field"))
      {
        var obj = new TestClass();
        expression.AddObjectInScope("obj", obj);
        var initialTicks = Environment.TickCount;
        for (obj.int_Field = 1; obj.int_Field < 1000000; obj.int_Field++)
          Assert.AreEqual(1 + obj.int_Field, expression.Execute());
        Assert.Less(Environment.TickCount - initialTicks, 2000); // 1MM iterations should run in less than 1 second
      }
    }

    [Test]
    public void ExpressionCountPressure_Success()
    {
      for (var i = 1; i < 50; i++)
      {
        using (var expression = new CSharpScript(String.Format("{0} + 1", i)))
          Assert.AreEqual(1 + i, expression.Execute());
      }
    }

    private static CSharpScript Compile(string _expression)
    {
      var expression = new CSharpScript(_expression);
      expression.Prepare();
      return expression;
    }

    [Test]
    public async void ExpressionParalellCompilation_Success()
    {
      var tasks = new Task[4]; // We will compile expressions in 4 threads
      for (var i = 1; i < 25; i++)
      {
        var expressions = new List<CSharpScript>();
        for (var j = 0; j < tasks.Length - 1; j++)
          tasks[j] = Task.Run(() => Compile("1 + 1"));
        for (var j = 0; j < tasks.Length - 1; j++)
        {
          var expression = await (Task<CSharpScript>)tasks[j];
          expressions.Add(expression);
          Assert.AreEqual(2, expression.Execute());
        }
        foreach (var expr in expressions)
          expr.Dispose();
      }
    }

    [Test]
    public void StoreExpressionInDictionary_Success()
    {
      var Dict = new Dictionary<string, CSharpScript>();
      using (var expr1 = new CSharpScript("1 + 1"))
      {
        Dict.Add(expr1.ProgramText, expr1);
        CSharpScript expr2;
        Assert.True(Dict.TryGetValue(expr1.ProgramText, out expr2));
        Assert.AreEqual(2, expr2.Execute());
      }
    }

    [Test]
    public void TwoBasicExpressionsInOneObject_Success()
    {
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddExpression("1 + 1"));
        Assert.AreEqual(1, expression.AddExpression("2 + 2"));
        Assert.AreEqual(2, expression.Execute());
        Assert.AreEqual(4, expression.Execute(1));
      }
    }

    [Test]
    [ExpectedException("System.Exception", ExpectedMessage = "Invalid exprNo parameter")]
    public void RunNonExistingExpressionNumber_Failure()
    {
      using (var expression = new CSharpScript())
        expression.Execute(2);
    }

    [Test]
    public void OneThousendExpressionsInOneObject_Success()
    {
      using (var expression = new CSharpScript())
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
      using (var expression = new CSharpScript())
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
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddVoidReturnCodeSnippet("global.a = 0"));
        Assert.AreEqual(1, expression.AddExpression("global.a++"));
        Assert.AreEqual(null, expression.Execute()); // setup the global
        var initialTicks = Environment.TickCount;
        for (uint i = 0; i < 1000000; i++)
          Assert.AreEqual(i, expression.Execute(1));
        Assert.Less(Environment.TickCount - initialTicks, 3000); // 1 million executions in less than 3 seconds
      }
    }

    [Test]
    public void CallInjectedFunction_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddMember(
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
      using (var expression = new CSharpScript("1 + 1"))
      {
        expression.ExecuteInSeparateAppDomain = true;
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void BasicExpressionSeparateAppDomainShareIntegerObject_Success()
    {
      using (var expression = new CSharpScript("1 + a"))
      {
        expression.ExecuteInSeparateAppDomain = true;
        expression.AddObjectInScope("a", 1);
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    public void UseExpandoObjectWithDynamicMethod_Success()
    {
      using (var expression = new CSharpScript("\"a\" + a.Test(\"a\")"))
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
      using (var expression = new CSharpScript())
      {
        var expando = SovosExpandoBuilder.Build();
        expression.AddObjectInScope("sovosExpando", expando);
        expression.AddCodeSnippet(
          @"var v = sovosExpando.Test; // We read here a property that gets created on-the-fly
            sovosExpando.Test = ""Hola Mundo""; // We enter a value here that will be cleared by ResetTest() call
            sovosExpando.ResetTest(); // We invoke a dynamically created method here
            sovosExpando.Test = v + sovosExpando.Test + ""Hello World""; // We use here the property read on-the-fly and stored in v
            return sovosExpando.Test");
        Assert.AreEqual("Hello World", expression.Execute());
      }
    }

    public void ClassSnippet_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddMember(
          @"private class Tester {
            public static int Test() {
              return 10;
            }
          }");
        Assert.AreEqual(0, expression.AddCodeSnippet("var i = 1; return Tester.Test() + i"));
        Assert.AreEqual(11, expression.Execute());
      }
    }

    [Test]
    public void InvokeMethodTwice_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddMember(
          @"public int Test() {
              return 10;            
            }");
        Assert.AreEqual(10, expression.Invoke("Test", null));
        Assert.AreEqual(10, expression.Invoke("Test", null));
      }
    }

    [Test]
    public void InvokeMethodWithParams_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddMember(
          @"public int Test(int a) {       
              return a;            
            }");
        Assert.AreEqual(5, expression.Invoke("Test", new object[]{5}));
      }
    }

    [Test]
    public void InvokeMethodWithParamsModifyGlobalsMultiAppDomain_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.ExecuteInSeparateAppDomain = true;
        expression.AddMember(
          @"public int Test(int a) {  
              global.a = 20;     
              return a;            
            }");
        expression.AddExpression("global.a");
        Assert.AreEqual(5, expression.Invoke("Test", new object[] { 5 }));
        Assert.AreEqual(20, expression.Execute());
      }
    }

    [Test]
    public void InvokeVoidMethodWithParamsModifyGlobals_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddMember(
          @"public void Test(int a) {  
              global.a = a;                            
            }");
        expression.AddExpression("global.a");
        Assert.AreEqual(null, expression.Invoke("Test", new object[] { 5 }));
        Assert.AreEqual(5, expression.Execute());
      }
    }

    [Test]
    public void CreateTwoObjectsMaintainingDifferentState_Success()
    {
      using (var script = new CSharpScript())
      {
        script.AddMember(
          @"public void Test(int a) {  
              global.a = a;                            
            }");
        script.AddExpression("global.a");
        var obj_a = script.CreateScriptObject();
        var obj_b = script.CreateScriptObject();
        Assert.AreEqual(null, script.Invoke(obj_a, "Test", new object[] { 5 }));
        Assert.AreEqual(null, script.Invoke(obj_b, "Test", new object[] { 10 }));
        Assert.AreEqual(5, script.Execute(obj_a));
        Assert.AreEqual(10, script.Execute(obj_b));
      }
    }

    [Test]
    public void CreateObjectOfNestedClassWithinScriptObject_Success()
    {
      using (var script = new CSharpScript())
      {
        script.AddMember(
          @"public class TestClass {  
              public int a = 10;  
              public TestClass() {
              }                          
            }
          ");
        dynamic obj = script.CreateScriptObject("TestClass");
        Assert.AreEqual(10, obj.a);
      }
    }

    [Test]
    public void CreateObjectOfNestedClassWithinScriptObjectUsingSeparateAppDomain_Success()
    {
      using (var script = new CSharpScript())
      {
        script.ExecuteInSeparateAppDomain = true;
        script.AddMember(
          @"public class TestClass : CSharpScriptObjectBase {  
              public int _a = 10;  
              public TestClass() {
              }           
              public int a() {
                return _a;
              }               
            }
          ");
        var obj = (ICSharpScriptObjectMethodInvoker)script.CreateScriptObject("TestClass");
        Assert.AreEqual(10, obj.Invoke("a", null));
      }
    }

    [Test]
    public void CreateExceptionObjectOfNestedClassWithinScriptAndThrow_Success()
    {
      using (var script = new CSharpScript())
      {
        script.AddMember(
          @"[Serializable]
            public class ETestClass : CSharpScriptException {}
          ");
        var obj = (Exception)script.CreateScriptObject("ETestClass");
        /* access SetMessage() using ICSharpScriptObjectMethodInvoker */
        var setter = (ICSharpScriptObjectMethodInvoker)obj;
        setter.Invoke("SetMessage", new object[]{"Hello World"});
        /* access SetMessage() using dynamic object */
        dynamic dynObj = obj;
        dynObj.SetMessage("Hello World");
        try
        {
          throw obj;
        }
        catch(Exception e)
        {
          Assert.AreEqual("ETestClass", e.GetType().Name);
          Assert.AreEqual("Hello World", e.Message);
        }
      }
    }

    [Test]
    [ExpectedException("System.Runtime.Serialization.SerializationException")]
    public void CreateExceptionObjectOfNestedClassWithinScriptSeparateAppDomain_Failure()
    {
      using (var script = new CSharpScript())
      {
        script.ExecuteInSeparateAppDomain = true;
        script.AddMember(
          @"[Serializable]
            public class ETestClass : Exception {}
          ");
        script.CreateScriptObject("ETestClass");
      }
    }

    [Test]
    public void CreateTwoObjectsWithObjectInScopeMaintainingDifferentState_Success()
    {
      using (var script = new CSharpScript())
      {
        script.AddMember(
          @"public void Test(int a) {  
              global.a = a;                            
            }");
        script.AddExpression("global.a + i");
        script.AddObjectInScope("i", 1);
        var obj_a = script.CreateScriptObject();
        var obj_b = script.CreateScriptObject();
        Assert.AreEqual(null, script.Invoke(obj_a, "Test", new object[] { 5 }));
        Assert.AreEqual(null, script.Invoke(obj_b, "Test", new object[] { 10 }));
        Assert.AreEqual(6, script.Execute(obj_a));
        Assert.AreEqual(11, script.Execute(obj_b));
      }
    }

    [Test]
    public void CreateTwoObjectsWithObjectInScopeResettingIt_Success()
    {
      using (var script = new CSharpScript())
      {
        script.AddMember(
          @"public void Test(int a) {  
              global.a = a;                            
            }");
        script.AddExpression("global.a + i");
        script.AddObjectInScope("i", 1);
        var obj_a = script.CreateScriptObject();
        var obj_b = script.CreateScriptObject();
        Assert.AreEqual(null, script.Invoke(obj_a, "Test", new object[] { 5 }));
        Assert.AreEqual(null, script.Invoke(obj_b, "Test", new object[] { 10 }));
        Assert.AreEqual(6, script.Execute(obj_a));
        Assert.AreEqual(11, script.Execute(obj_b));
        script.ReplaceObjectInScope(obj_a, "i", 2);
        Assert.AreEqual(7, script.Execute(obj_a));
        Assert.AreEqual(11, script.Execute(obj_b));
      }
    }

    [Test]
    public void AddSameExpressionTwice_Success()
    {
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddExpression("1 + 1"));
        Assert.AreEqual(0, expression.AddExpression("1 + 1"));
        Assert.AreEqual(2, expression.Execute());
      }
    }

    [Test]
    [ExpectedException("System.Exception", ExpectedMessage = "Invalid exprNo parameter")]
    public void AddSameExpressionTwiceAssumeDuplication_Failure()
    {
      using (var expression = new CSharpScript())
      {
        Assert.AreEqual(0, expression.AddExpression("1 + 1"));
        Assert.AreEqual(0, expression.AddExpression(" 1 + 1 "));
        Assert.AreEqual(2, expression.Execute());
        expression.Execute(1); // This will throw the final exception expected by the test
      }
    }

    [Test]
    public void AddSameUsedNamespaceTwice_Success()
    {
      using (var expression = new CSharpScript())
      {
        expression.AddUsedNamespace("System.Collections");
        expression.AddUsedNamespace("System.Collections");
        expression.GenerateCode();
        expression.Compile();
        Assert.True(true); // If we go here with no exception, we are good
      }
    }
  }
}
