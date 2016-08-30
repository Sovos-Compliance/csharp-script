namespace CSharpCodeEvaluatorTestClass
{
  public class TestClassInGACAssembly
  {
    public int a;

    public TestClassInGACAssembly()
    {
      a = 10;
    }

    int Inc()
    {
      return ++a;
    }
  }
}
