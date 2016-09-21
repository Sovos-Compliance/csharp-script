using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Oracle.DataAccess.Client;
using Sovos.Scripting;

namespace csharp_code_evaluator_ut
{
  class CSharpCodeOracleTest
  {
    [Test]
    public void BasicOracleOpen_Success()
    {
      string CodeSnippet = @"string ConnectionString = ""User Id=ceenv100;Password=AfCGMp5Z8gglqc080L9E;Data Source=p11d1;"";
                            string QueryString = ""Select '1' Cnt from Dual"";
                            List<string> SnList = new List<string>();
                            SnList.Clear();
                            using (var conn = new OracleConnection { ConnectionString = ConnectionString })
                            {
                                conn.Open();
                                using (var cmd = conn.CreateCommand())
                                {
                                    cmd.CommandText = QueryString;
                                    var dr = cmd.ExecuteReader();
                                    while (dr.Read())
                                    {
                                        SnList.Add(dr.GetString(0).ToString());
                                    }
                                }
                            }
                            return SnList.Count";
      using (var expression = new CSharpScript())
      {
        expression.AddCodeSnippet(CodeSnippet);
        Assert.AreEqual(1, expression.Execute());
      }
    }

    [Test]
    public void BasicOracleAddObject_Success()
    {
      string ConnectionString = "User Id=ceenv100;Password=AfCGMp5Z8gglqc080L9E;Data Source=p11d1;";
      string CodeSnippet = @"string QueryString = ""Select '1' Cnt from Dual"";
                            List<string> SnList = new List<string>();
                            SnList.Clear();                                
                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = QueryString;
                                var dr = cmd.ExecuteReader();
                                while (dr.Read())
                                {
                                    SnList.Add(dr.GetString(0).ToString());
                                }
                            }

                            return SnList.Count";
      using (var expression = new CSharpScript())
      {
        using (var conn = new OracleConnection {ConnectionString = ConnectionString})
        {
          conn.Open();
          expression.AddObjectInScope("conn", conn);
          expression.AddCodeSnippet(CodeSnippet);
          Assert.AreEqual(1, expression.Execute());
        }
      }
    }
  }
}
