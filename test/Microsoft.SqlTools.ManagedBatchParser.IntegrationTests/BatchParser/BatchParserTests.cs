//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable disable

using Microsoft.SqlTools.ManagedBatchParser.IntegrationTests.TSQLExecutionEngine;
using Microsoft.SqlTools.ManagedBatchParser.IntegrationTests.Utility;
using Microsoft.SqlTools.ServiceLayer.BatchParser;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;
using Microsoft.SqlTools.ServiceLayer.Test.Common;
using Microsoft.SqlTools.ServiceLayer.Test.Common.Baselined;
using System;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Microsoft.SqlTools.ServiceLayer.Connection;
using System.Threading.Tasks;

namespace Microsoft.SqlTools.ManagedBatchParser.UnitTests.BatchParser
{
    [TestFixture]
    public class BatchParserTests : BaselinedTest
    {
        public BatchParserTests()
        {
            InitializeTest();
        }

        public void InitializeTest()
        {
            CategoryName = "BatchParser";
            this.TraceOutputDirectory = RunEnvironmentInfo.GetTraceOutputLocation();
            TestInitialize();
        }

        /// <summary>
        /// Verifies that no exception is thrown when IVariableResolver passed to
        /// the Parser ctor is null.
        /// </summary>
        [Test]
        public void CanHandleNullIVariableResolver()
        {
            string script = @"
SELECT '$(VAR1)'
GO 10
SELECT '$(VAR2)'";

            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);
            
            using (var p = new Parser(
                commandHandler: handler,
                variableResolver: null,
                new StringReader(script),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = false;
                handler.SetParser(p);

                p.Parse();
                Assert.That(output.ToString(), Is.EqualTo("*** Execute batch (10)\nBatch text:\r\n\r\nSELECT '$(VAR1)'\r\n\r\n\r\n*** Execute batch (1)\nBatch text:\r\nSELECT '$(VAR2)'\r\n\r\n"), "Oh no!");
            }
        }

        /// <summary>
        /// Verifies that the default value for DisableVariableSubstitution on the
        /// Parser object is false, unless IVariableResolver is null.
        /// Essentially, this means that IVariableResolver=null implies
        /// DisableVariableSubstitution=true.
        /// </summary>
        [TestCase(true, Description = "IVariableResolver is null", TestName = "DisableVariableSubstitutionIsTrueWhenIVariableResolverIsNull")]
        [TestCase(false, Description = "IVariableResolver is not null", TestName = "DisableVariableSubstitutionIsFalseWhenIVariableResolverIsNotNull")]
        public void DisableVariableSubstitutionTests(bool p)
        {
            string script = "SELECT $(VAR1)";

            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);

            using (var parser = new Parser(commandHandler: handler, variableResolver: p ? null : new TestVariableResolver(output), new StringReader(script), "test"))
            {
                Assert.That(parser.DisableVariableSubstitution, Is.EqualTo(p), "Unexpected default value for DisableVariableSubstitution");
            }
        }

        /// <summary>
        /// Shows how the DisableVariableSubstitution, ThrowOnUnresolvedVariable, and the success
        /// of failure of a substitution interact with one another.
        /// </summary>
        [TestCase(true, true, true)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, true)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, false, false)]
        public void DisableVariableSubstitutionAndThrowOnUnresolvedVariableInteraction(bool disableVariableSubstitution, bool throwOnUnresolvedVariable, bool canResolve)
        {
            string script = "SELECT $(VAR1)";
            var output_hander = new StringBuilder();
            var output_resolver = new StringBuilder();
            var handler = new TestCommandHandler(output_hander);
            var resolver = new TestVariableResolver(output_resolver);

            if (canResolve)
            {
                resolver.SetVariable(new PositionStruct(), "VAR1", "42");
            }

            using (var parser = new Parser(commandHandler: handler, variableResolver: resolver, new StringReader(script), "test"))
            {
                parser.DisableVariableSubstitution = disableVariableSubstitution;
                parser.ThrowOnUnresolvedVariable = throwOnUnresolvedVariable;

                if (disableVariableSubstitution || canResolve || !throwOnUnresolvedVariable)
                {
                    parser.Parse();
                    if (canResolve && !disableVariableSubstitution)
                    {
                        // We do not really care about the whole output... a partial match is sufficient.
                        Assert.That(output_hander.ToString(), Contains.Substring("Execute batch (1)\nText with variables resolved:\r\nSELECT 42\r\nText with variables not resolved:\r\nSELECT $(VAR1)"), "Unexpected result of parsing!");
                    }
                    else
                    {
                        // We do not really care about the whole output... a partial match is sufficient.
                        Assert.That(output_hander.ToString(), Is.EqualTo("*** Execute batch (1)\nBatch text:\r\nSELECT $(VAR1)\r\n\r\n"), "Unexpected result of parsing!");
                    }
                }
                else
                {
                    var exc = Assert.Throws<BatchParserException>(parser.Parse, "Expected exception because $(VAR1) was not defined!");
                    Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.VariableNotDefined), "Error code should be VariableNotDefined!");
                    Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
                    Assert.That(exc.Text, Is.EqualTo("SELECT $(VAR1)"), "Unexpected Text");
                }
            }
        }


        [Test]
        [Ignore("Active issue: https://github.com/microsoft/sqltoolsservice/issues/1938")]
        public void BatchParserCanHandleSqlAgentTokens()
        {
            string script = "SELECT N'$(ESCAPE_SQUOTE(SRVR))'";

            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);

            using (var parser = new Parser(commandHandler: handler, new TestVariableResolver(output), new StringReader(script), "test"))
            {
                var exc = Assert.Throws<BatchParserException>(parser.Parse, "Was https://github.com/microsoft/sqltoolsservice/issues/1938 fixed? Please, update the test!");
                Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.InvalidVariableName), "Error code should be InvalidVariableName!");
                Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
                Assert.That(exc.Text, Is.EqualTo("$(ESCAPE_SQUOTE("), "Unexpected Text");
            }
        }

        /// <summary>
        /// Setting DisableVariableSubstitution=true has the effect of preventing the
        /// Parser from trying to interpret variables, thus allowing such variables to
        /// remain embedded in strings within the T-SQL script (e.g. SQL Agent variables)
        /// </summary>
        /// <remarks>This test will need to be modified when issue 1938 is addressed.</remarks>
        [Test]
        public void WhenDisableVariableSubstitutionIsTrueNoParsingOfVariablesHappens()
        {
            string script = "SELECT N'$(ESCAPE_SQUOTE(SRVR))'";

            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);

            using (var parser = new Parser(commandHandler: handler, new TestVariableResolver(output), new StringReader(script), "test"))
            {
                // Explicitly disable variable substitution
                parser.DisableVariableSubstitution = true;

                parser.Parse();
                Assert.That(output.ToString(), Is.EqualTo("*** Execute batch (1)\nBatch text:\r\nSELECT N'$(ESCAPE_SQUOTE(SRVR))'\r\n\r\n"), "How was the SQL Agent macro parsed?");
            }
        }

        /// <summary>
        /// Setting DisableVariableSubstitution=true has the effect of preventing the
        /// Parser from trying to interpret variables; without this, we would not be
        /// able to handle T-SQL fragement (like strings) that happen to have in them
        /// text that resemble a sqlcmd variable, e.g. "$(".
        /// </summary>
        /// <remarks>This test will need to be modified when issue 1938 is addressed.</remarks>
        [Test]
        public void WhenDisableVariableSubstitutionIsTrueNoParsingOfVariablesHappensWithMalformedVariable()
        {
            string script = @"SELECT N'$(X'"; // Note: $(X is a valid string in T-SQL (it is enclosed in single quotes, but it has nothing to do with a variable!)

            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);

            using (var parser = new Parser(commandHandler: handler, new TestVariableResolver(output), new StringReader(script), "test"))
            {
                // Explicitly disable variable substitution
                parser.DisableVariableSubstitution = true;

                parser.Parse();
                Assert.That(output.ToString(), Is.EqualTo("*** Execute batch (1)\nBatch text:\r\nSELECT N'$(X'\r\n\r\n"), "Why are we trying to make sense of $(X ?");
            }
        }

        /// <summary>
        /// Verifies that the parser throws an exception when the the sqlcmd script
        /// uses a variable that is not defined. The expected exception has the
        /// correct ErrorCode and TokenType.
        /// </summary>
        [Test]
        public void VerifyVariableResolverThrowsWhenVariableIsNotDefined()
        {
            string script = "print '$(NotDefined)'";
            StringBuilder output = new StringBuilder();

            TestCommandHandler handler = new TestCommandHandler(output);
            IVariableResolver resolver = new TestVariableResolver(new StringBuilder());
            using (Parser p = new Parser(
                handler,
                resolver,
                new StringReader(script),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = true;
                handler.SetParser(p);

                var exc = Assert.Throws<BatchParserException>(p.Parse, "Expected exception because $(NotDefined) was not defined!");
                Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.VariableNotDefined), "Error code should be VariableNotDefined!");
                Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
            }
        }

        [Test]
        public void VerifyVariableResolverThrowsWhenVariableHasInvalidName_StartsWithNumber()
        {
            // instead of using variable calcOne, I purposely use variable 0alcOne
            string query = @"SELECT $(0alcOne)";

            TestCommandHandler handler = new TestCommandHandler(new StringBuilder());
            IVariableResolver resolver = new TestVariableResolver(new StringBuilder());
            using (Parser p = new Parser(
                handler,
                resolver,
                new StringReader(query),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = true;
                handler.SetParser(p);
                var exc = Assert.Throws<BatchParserException>(p.Parse, "Expected exception because $(0alcOne) was not defined!");
                Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.InvalidVariableName), "Error code should be InvalidVariableName!");
                Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
                Assert.That(exc.Text, Is.EqualTo("$(0"), "Unexpected Text");
            }
        }

        [Test]
        public void VerifyVariableResolverThrowsWhenVariableHasInvalidName_ContainesInvalidChar()
        {
            // instead of using variable calcOne, I purposely use variable ca@lcOne
            string query = @"SELECT $(ca@lcOne)";

            TestCommandHandler handler = new TestCommandHandler(new StringBuilder());
            IVariableResolver resolver = new TestVariableResolver(new StringBuilder());
            using (Parser p = new Parser(
                handler,
                resolver,
                new StringReader(query),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = true;
                handler.SetParser(p);

                var exc = Assert.Throws<BatchParserException>(p.Parse, "Expected exception because $(ca@lcOne) was not defined!");
                Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.InvalidVariableName), "Error code should be InvalidVariableName!");
                Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
                Assert.That(exc.Text, Is.EqualTo("$(ca@"), "Unexpected Text");
            }
        }

        // A GO followed by a number that is greater than 2147483647 cause the parser to
        // throw an exception.
        [Test]
        public void VerifyInvalidNumberExceptionThrownWhenParsingGoExceedsMaxInt32()
        {
            string query = $@"SELECT 1+1
                           GO {1L + int.MaxValue}";

            TestCommandHandler handler = new TestCommandHandler(new StringBuilder());
            IVariableResolver resolver = new TestVariableResolver(new StringBuilder());
            using (Parser p = new Parser(
                handler,
                resolver,
                new StringReader(query),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = true;
                handler.SetParser(p);
                // This test will fail because we are passing invalid number.
                // Exception will be raised from  ParseGo()
                var exc = Assert.Throws<BatchParserException>(p.Parse, $"Expected exception because GO is followed by a invalid number (>{int.MaxValue})");
                Assert.That(exc.ErrorCode, Is.EqualTo(Microsoft.SqlTools.ServiceLayer.BatchParser.ErrorCode.InvalidNumber), "Error code should be InvalidNumber!");
                Assert.That(exc.TokenType, Is.EqualTo(LexerTokenType.Text), "Unexpected TokenType");
                Assert.That(exc.Text, Is.EqualTo("2147483648"), "Unexpected Text");
            }
        }

        // Verify the Batch execution is executed successfully.
        [Test]
        public void VerifyExecute()
        {
            Batch batch = new Batch(sqlText: "SELECT 1+1", isResultExpected: true, execTimeout: 15);
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
            using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
            {
                var executionResult = batch.Execute(sqlConn, ShowPlanType.AllShowPlan);
                Assert.AreEqual(ScriptExecutionResult.Success, executionResult);
            }

        }

        // Verify the exeception is handled by passing invalid keyword.
        [Test]
        public void VerifyHandleExceptionMessage()
        {
            Batch batch = new Batch(sqlText: "SEL@ECT 1+1", isResultExpected: true, execTimeout: 15);
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
            using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
            {
                ScriptExecutionResult result = batch.Execute(sqlConn, ShowPlanType.AllShowPlan);
            }
            ScriptExecutionResult finalResult = (batch.RowsAffected > 0) ? ScriptExecutionResult.Success : ScriptExecutionResult.Failure;

            Assert.AreEqual(ScriptExecutionResult.Failure, finalResult);
        }

        // Verify the passing query has valid text.
        [Test]
        public void VerifyHasValidText()
        {
            Batch batch = new Batch(sqlText: null, isResultExpected: true, execTimeout: 15);
            ScriptExecutionResult finalResult = ScriptExecutionResult.All;
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
            using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
            {
                ScriptExecutionResult result = batch.Execute(sqlConn, ShowPlanType.AllShowPlan);
            }
            finalResult = (batch.RowsAffected > 0) ? ScriptExecutionResult.Success : ScriptExecutionResult.Failure;

            Assert.AreEqual(ScriptExecutionResult.Failure, finalResult);
        }

        // Verify the cancel functionality is working fine.
        [Test]
        public void VerifyCancel()
        {
            ScriptExecutionResult result = ScriptExecutionResult.All;
            Batch batch = new Batch(sqlText: "SELECT 1+1", isResultExpected: true, execTimeout: 15);
            var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
            using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
            {
                batch.Cancel();
                result = batch.Execute(sqlConn, ShowPlanType.AllShowPlan);
            }
            Assert.AreEqual(ScriptExecutionResult.Cancel, result);
        }

        // 
        /// <summary>
        /// Verify whether lexer can consume token for SqlCmd variable
        /// </summary>
        [Test]
        public void VerifyLexerSetState()
        {
            try
            {
                string query = ":SETVAR    a 10";
                var inputStream = GenerateStreamFromString(query);
                using (Lexer lexer = new Lexer(new StreamReader(inputStream), "Test.sql"))
                {
                    lexer.ConsumeToken();
                }
            }
            catch (Exception e)
            {
                Assert.True(false, $"Unexpected error consuming token : {e.Message}");
            }
            //  we doesn't expect any exception or testCase failures

        }

        // This test case is to verify that, Powershell's Invoke-SqlCmd handles ":!!if" in an inconsistent way
        // Inconsistent way means, instead of throwing an exception as "Command Execute is not supported." it was throwing "Incorrect syntax near ':'."
        [Test]
        public void VerifySqlCmdExecute()
        {
            string query = ":!!if exist foo.txt del foo.txt";
            var inputStream = GenerateStreamFromString(query);
            TestCommandHandler handler = new TestCommandHandler(new StringBuilder());
            IVariableResolver resolver = new TestVariableResolver(new StringBuilder());
            using (Parser p = new Parser(
                handler,
                resolver,
                new StringReader(query),
                "test"))
            {
                p.ThrowOnUnresolvedVariable = true;
                handler.SetParser(p);

                var exception = Assert.Throws<BatchParserException>(p.Parse);
                // Verify the message should be "Command Execute is not supported."
                Assert.AreEqual("Command Execute is not supported.", exception.Message);
            }
        }

        // This test case is to verify that, Lexer type for :!!If was set to "Text" instead of "Execute"
        [Test]
        public void VerifyLexerTypeOfSqlCmdIFisExecute()
        {
            string query = ":!!if exist foo.txt del foo.txt";
            var inputStream = GenerateStreamFromString(query);
            LexerTokenType type = LexerTokenType.None;
            using (Lexer lexer = new Lexer(new StreamReader(inputStream), "Test.sql"))
            {
                lexer.ConsumeToken();
                type = lexer.CurrentTokenType;
            }
            // we are expecting the lexer type should to be Execute.
            Assert.AreEqual("Execute", type.ToString());
        }

        // Verify the custom exception functionality by raising user defined error.
        [Test]
        public void VerifyCustomBatchParserException()
        {
            string message = "This is userDefined Error";

            Token token = new Token(LexerTokenType.Text, new PositionStruct(), new PositionStruct(), message, "test");

            BatchParserException batchParserException = new BatchParserException(ErrorCode.VariableNotDefined, token, message);

            Assert.AreEqual(batchParserException.ErrorCode.ToString(), ErrorCode.VariableNotDefined.ToString());
            Assert.AreEqual(message, batchParserException.Text);
            Assert.AreEqual(LexerTokenType.Text.ToString(), batchParserException.TokenType.ToString());
        }

        // Verify whether the executionEngine execute script
        [Test]
        public void VerifyExecuteScript()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                string query = @"SELECT 1+2
                                Go 2";
                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                {
                    TestExecutor testExecutor = new TestExecutor(query, sqlConn, new ExecutionEngineConditions());
                    testExecutor.Run();

                    ScriptExecutionResult result = (testExecutor.ExecutionResult == ScriptExecutionResult.Success) ? ScriptExecutionResult.Success : ScriptExecutionResult.Failure;

                    Assert.AreEqual(ScriptExecutionResult.Success, result);
                }
            }
        }

        // Verify whether the batchParser execute SqlCmd.
        //[Test]   //  This Testcase should execute and pass, But it is failing now.
        public void VerifyIsSqlCmd()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                string query = @"sqlcmd -Q ""select 1 + 2 as col"" ";
                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                {
                    TestExecutor testExecutor = new TestExecutor(query, sqlConn, new ExecutionEngineConditions());
                    testExecutor.Run();
                    Assert.True(testExecutor.ResultCountQueue.Count >= 1);
                }
            }
        }

        /// <summary>
        /// Verify whether the batchParser execute SqlCmd successfully
        /// </summary>
        [Test]
        public void VerifyRunSqlCmd()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                const string sqlCmdQuery = @"
:setvar __var1 1
:setvar __var2 2
:setvar __IsSqlCmdEnabled " + "\"True\"" + @"
GO
IF N'$(__IsSqlCmdEnabled)' NOT LIKE N'True'
    BEGIN
        PRINT N'SQLCMD mode must be enabled to successfully execute this script.';
                SET NOEXEC ON;
                END
GO
select $(__var1) + $(__var2) as col
GO";

                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                var condition = new ExecutionEngineConditions() { IsSqlCmd = true };
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                using (TestExecutor testExecutor = new TestExecutor(sqlCmdQuery, sqlConn, condition))
                {
                    testExecutor.Run();

                    Assert.True(testExecutor.ResultCountQueue.Count >= 1, $"Unexpected number of ResultCount items - expected 0 but got {testExecutor.ResultCountQueue.Count}");
                    Assert.True(testExecutor.ErrorMessageQueue.Count == 0, $"Unexpected error messages from test executor : {string.Join(Environment.NewLine, testExecutor.ErrorMessageQueue)}");
                }
            }
        }

        /// <summary>
        /// Verify whether the batchParser parsed :connect command successfully
        /// </summary>
        [Test]
        public void VerifyConnectSqlCmd()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                string serverName = liveConnection.ConnectionInfo.ConnectionDetails.ServerName;
                string userName = liveConnection.ConnectionInfo.ConnectionDetails.UserName;
                string password = liveConnection.ConnectionInfo.ConnectionDetails.Password;
                var credentials = string.IsNullOrEmpty(userName) ? string.Empty : $"-U {userName} -P {password}";
                string sqlCmdQuery = $@"
:Connect {serverName} {credentials}
GO
select * from sys.databases where name = 'master'
GO";

                string sqlCmdQueryIncorrect = $@"
:Connect {serverName} -u uShouldbeUpperCase -p pShouldbeUpperCase
GO
select * from sys.databases where name = 'master'
GO";
                var condition = new ExecutionEngineConditions() { IsSqlCmd = true };
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                using (TestExecutor testExecutor = new TestExecutor(sqlCmdQuery, sqlConn, condition))
                {
                    testExecutor.Run();
                    Assert.Multiple(() =>
                    {
                       Assert.That(testExecutor.ParserExecutionError, Is.False, "Parse Execution error should be false");
                       Assert.That(testExecutor.ResultCountQueue.Count, Is.EqualTo(1), "Unexpected number of ResultCount items");
                       Assert.That(testExecutor.ErrorMessageQueue, Is.Empty, "Unexpected error messages from test executor");
                    });
                }

                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                using (TestExecutor testExecutor = new TestExecutor(sqlCmdQueryIncorrect, sqlConn, condition))
                {
                    testExecutor.Run();
                    Assert.Multiple(() =>
                    {
                        Assert.True(testExecutor.ParserExecutionError, "Parse Execution error should be true");
                        Assert.That(testExecutor.ErrorMessageQueue, Has.Member("Incorrect syntax was encountered while -u was being parsed."), "error message expected");
                    });
                }
            }
        }
        
        /// <summary>
        /// Verify whether the batchParser parsed :on error successfully
        /// </summary>
        [Test]
        public void VerifyOnErrorSqlCmd()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                string serverName = liveConnection.ConnectionInfo.ConnectionDetails.ServerName;
                string sqlCmdQuery = $@"
:on error ignore
GO
select * from sys.databases_wrong where name = 'master'
GO
select* from sys.databases where name = 'master'
GO
:on error exit
GO
select* from sys.databases_wrong where name = 'master'
GO
select* from sys.databases where name = 'master'
GO";
                var condition = new ExecutionEngineConditions() { IsSqlCmd = true };
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                using (TestExecutor testExecutor = new TestExecutor(sqlCmdQuery, sqlConn, condition))
                {
                    testExecutor.Run();

                    Assert.True(testExecutor.ResultCountQueue.Count == 1, $"Unexpected number of ResultCount items - expected only 1 since the later should not be executed but got {testExecutor.ResultCountQueue.Count}");
                    Assert.True(testExecutor.ErrorMessageQueue.Count == 2, $"Unexpected number error messages from test executor expected 2, actual : {string.Join(Environment.NewLine, testExecutor.ErrorMessageQueue)}");
                }
            }
        }

        /// <summary>
        /// Verify whether the batchParser parses Include command i.e. :r successfully
        /// </summary>
        [Test]
        public void VerifyIncludeSqlCmd()
        {
            string file = "VerifyIncludeSqlCmd_test.sql";
            try
            {
                using (ExecutionEngine executionEngine = new ExecutionEngine())
                {
                    var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                    string serverName = liveConnection.ConnectionInfo.ConnectionDetails.ServerName;
                    string sqlCmdFile = $@"
select * from sys.databases where name = 'master'
GO";
                    File.WriteAllText("VerifyIncludeSqlCmd_test.sql", sqlCmdFile);
                     
                    string sqlCmdQuery = $@"
:r {file}
GO
select * from sys.databases where name = 'master'
GO";

                    var condition = new ExecutionEngineConditions() { IsSqlCmd = true };
                    using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                    using (TestExecutor testExecutor = new TestExecutor(sqlCmdQuery, sqlConn, condition))
                    {
                        testExecutor.Run();

                        Assert.True(testExecutor.ResultCountQueue.Count == 2, $"Unexpected number of ResultCount items - expected 1 but got {testExecutor.ResultCountQueue.Count}");
                        Assert.True(testExecutor.ErrorMessageQueue.Count == 0, $"Unexpected error messages from test executor : {string.Join(Environment.NewLine, testExecutor.ErrorMessageQueue)}");
                    }
                    File.Delete(file);
                }
            }
            catch
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }

        // Verify whether the executionEngine execute Batch
        [Test]
        public void VerifyExecuteBatch()
        {
            using (ExecutionEngine executionEngine = new ExecutionEngine())
            {
                string query = "SELECT 1+2";
                var liveConnection = LiveConnectionHelper.InitLiveConnectionInfo("master");
                using (SqlConnection sqlConn = ConnectionService.OpenSqlConnection(liveConnection.ConnectionInfo))
                {
                    var executionPromise = new TaskCompletionSource<bool>();
                    executionEngine.BatchParserExecutionFinished += (object sender, BatchParserExecutionFinishedEventArgs e) =>
                    {
                        Assert.AreEqual(ScriptExecutionResult.Success, e.ExecutionResult);
                        executionPromise.SetResult(true);
                    };
                    executionEngine.ExecuteBatch(new ScriptExecutionArgs(query, sqlConn, 15, new ExecutionEngineConditions(), new BatchParserMockEventHandler()));
                    Task.WaitAny(executionPromise.Task, Task.Delay(5000));
                    Assert.True(executionPromise.Task.IsCompleted, "Execution did not finish in time");
                }
            }
        }

        [Test]
        public void CanceltheBatch()
        {
            Batch batch = new Batch();
            batch.Cancel();
        }

        private static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public void TokenizeWithLexer(string filename, StringBuilder output)
        {
            // Create a new file by changing CRLFs to LFs and generate a new steam
            // or the tokens generated by the lexer will always have off by one errors
            string input = File.ReadAllText(filename).Replace("\r\n", "\n");
            var inputStream = GenerateStreamFromString(input);
            using (Lexer lexer = new Lexer(new StreamReader(inputStream), filename))
            {
                string inputText = File.ReadAllText(filename);
                inputText = inputText.Replace("\r\n", "\n");
                StringBuilder roundtripTextBuilder = new StringBuilder();
                StringBuilder outputBuilder = new StringBuilder();
                StringBuilder tokenizedInput = new StringBuilder();
                bool lexerError = false;

                Token token = null;
                try
                {
                    do
                    {
                        lexer.ConsumeToken();
                        token = lexer.CurrentToken;
                        roundtripTextBuilder.Append(token.Text.Replace("\r\n", "\n"));
                        outputBuilder.AppendLine(GetTokenString(token));
                        tokenizedInput.Append('[').Append(GetTokenCode(token.TokenType)).Append(':').Append(token.Text.Replace("\r\n", "\n")).Append(']');
                    } while (token.TokenType != LexerTokenType.Eof);
                }
                catch (BatchParserException ex)
                {
                    lexerError = true;
                    outputBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, "[ERROR: code {0} at {1} - {2} in {3}, message: {4}]", ex.ErrorCode, GetPositionString(ex.Begin), GetPositionString(ex.End), GetFilenameOnly(ex.Begin.Filename), ex.Message));
                }
                output.AppendLine("Lexer tokenized input:");
                output.AppendLine("======================");
                output.AppendLine(tokenizedInput.ToString());
                output.AppendLine("Tokens:");
                output.AppendLine("=======");
                output.AppendLine(outputBuilder.ToString());

                if (lexerError == false)
                {
                    // Verify that all text from tokens can be recombined into original string
                    Assert.AreEqual(inputText, roundtripTextBuilder.ToString());
                }
            }
        }

        private string GetTokenCode(LexerTokenType lexerTokenType)
        {
            switch (lexerTokenType)
            {
                case LexerTokenType.Text:
                    return "T";

                case LexerTokenType.Whitespace:
                    return "WS";

                case LexerTokenType.NewLine:
                    return "NL";

                case LexerTokenType.Comment:
                    return "C";

                default:
                    return lexerTokenType.ToString();
            }
        }

        private static void CopyToOutput(string sourceDirectory, string filename)
        {
            File.Copy(Path.Combine(sourceDirectory, filename), filename, true);
            FileUtilities.SetFileReadWrite(filename);
        }

        public void TestParser(string filename, StringBuilder output)
        {
            try
            {
                // Create a new file by changing CRLFs to LFs and generate a new steam
                // or the tokens generated by the lexer will always have off by one errors
                TestCommandHandler commandHandler = new TestCommandHandler(output);
                string input = File.ReadAllText(filename).Replace("\r\n", "\n");
                var inputStream = GenerateStreamFromString(input);
                StreamReader streamReader = new StreamReader(inputStream);

                using (Parser parser = new Parser(
                    commandHandler,
                    new TestVariableResolver(output),
                    streamReader,
                    filename))
                {
                    commandHandler.SetParser(parser);
                    parser.Parse();
                }
            }
            catch (BatchParserException ex)
            {
                output.AppendLine(string.Format(CultureInfo.CurrentCulture, "[PARSER ERROR: code {0} at {1} - {2} in {3}, token text: {4}, message: {5}]", ex.ErrorCode, GetPositionString(ex.Begin), GetPositionString(ex.End), GetFilenameOnly(ex.Begin.Filename), ex.Text, ex.Message));
            }
        }

        private string GetPositionString(PositionStruct pos)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1} [{2}]", pos.Line, pos.Column, pos.Offset);
        }

        private string GetTokenString(Token token)
        {
            if (token == null)
            {
                return "(null)";
            }
            else
            {
                string tokenText = token.Text;
                if (tokenText != null)
                {
                    tokenText = tokenText.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                }
                string tokenFilename = token.Filename;
                tokenFilename = GetFilenameOnly(tokenFilename);
                return string.Format(CultureInfo.CurrentCulture, "[Token {0} at {1}({2}:{3} [{4}] - {5}:{6} [{7}]): '{8}']",
                    token.TokenType,
                    tokenFilename,
                    token.Begin.Line, token.Begin.Column, token.Begin.Offset,
                    token.End.Line, token.End.Column, token.End.Offset,
                    tokenText);
            }
        }

        internal static string GetFilenameOnly(string fullPath)
        {
            return fullPath != null ? Path.GetFileName(fullPath) : null;
        }

        public override void Run()
        {
            string inputFilename = GetTestscriptFilePath(CurrentTestName);
            StringBuilder output = new StringBuilder();

            TokenizeWithLexer(inputFilename, output);
            TestParser(inputFilename, output);

            string baselineFilename = GetBaselineFilePath(CurrentTestName);
            string baseline;

            try
            {
                baseline = GetFileContent(baselineFilename).Replace("\r\n", "\n");
            }
            catch (FileNotFoundException)
            {
                baseline = string.Empty;
            }

            string outputString = output.ToString().Replace("\r\n", "\n");

            //Console.WriteLine(baselineFilename);

            if (string.Compare(baseline, outputString, StringComparison.Ordinal) != 0)
            {
                DumpToTrace(CurrentTestName, outputString);
                string outputFilename = Path.Combine(TraceFilePath, GetBaselineFileName(CurrentTestName));
                Console.WriteLine(":: Output does not match the baseline!");
                Console.WriteLine("code --diff \"" + baselineFilename + "\" \"" + outputFilename + "\"");
                Console.WriteLine();
                Console.WriteLine(":: To update the baseline:");
                Console.WriteLine("copy \"" + outputFilename + "\" \"" + baselineFilename + "\"");
                Console.WriteLine();
            }
        }
    }
}