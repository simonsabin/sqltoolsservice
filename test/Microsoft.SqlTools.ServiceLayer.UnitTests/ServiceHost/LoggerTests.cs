//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable disable

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.SqlTools.ServiceLayer.Test.Common;
using Microsoft.SqlTools.Utility;
using NUnit.Framework;

namespace Microsoft.SqlTools.ServiceLayer.UnitTests.ServiceHost
{
    /// <summary>
    /// Logger test cases
    /// </summary>
    public class LoggerTests
    {
        /// <summary>
        /// Test to verify that the logger initialization is generating a valid file
        /// Verifies that a test log entries is succesfully written to a default log file.
        /// </summary>
        [Test]
        public void LoggerDefaultFile()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Information,
                TracingLevel = SourceLevels.Verbose,
            };

            test.Initialize();
            test.Write();
            test.Verify();
            test.Cleanup();
        }

        /// <summary>
        /// Test to verify that the logger initialization works using various possibilities.
        /// </summary>
        [Test]
        public void LoggerTestInitialization()
        {
            int? testNo = 1;
            //Test 1: Initialization with all defaults. Logfile names get autogenerated with the well known prefix.
            {
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                Logger.Initialize();
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 2: Initialization with TracingLevel set to Critical. Logfile names get autogenerated with the well known prefix. Before we do a write at Critical level the logfile must not get even created.
            {
                SourceLevels expectedTracingLevel = SourceLevels.Critical;
                string expectedTraceSource = Logger.defaultTraceSource;
                Logger.Initialize(expectedTracingLevel);
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 3: Initialization with TraceSourceName set to specified name. Logfile names get autogenerated with the well known prefix.
            {
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = nameof(LoggerTestInitialization);
                Logger.Initialize(traceSource:expectedTraceSource);
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }


            //Test 4: Initialization with logfile set to specified random filepath. 
            {
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                string logFilePath = Path.Combine(Path.GetRandomFileName(), nameof(LoggerTestInitialization));
                Logger.Initialize(traceSource: expectedTraceSource, logFilePath: logFilePath);
                Assert.True(string.Compare(logFilePath, Logger.LogFileFullPath, ignoreCase: true) == 0, "The logfile path of the Logger should be the one specified");
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, logFilePath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 5: Initialization with logfile generated from log directory and LogFilePrefix using Logger.GenerateLogFielPath method. 
            {
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                string logFilePath = Logger.GenerateLogFilePath(Path.Combine(Directory.GetCurrentDirectory(), nameof(LoggerTestInitialization)));
                Assert.True(string.Compare(Path.GetDirectoryName(logFilePath), Directory.GetCurrentDirectory(), ignoreCase: true) == 0, "The directory path of the logfile should match the directory path specified");
                Logger.Initialize(traceSource: expectedTraceSource, logFilePath: logFilePath);
                Assert.True(string.Compare(logFilePath, Logger.LogFileFullPath, ignoreCase: true) == 0, "The logfile path of the Logger should be the one specified");
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            #region TracingLevel Settings
            //Test 6: Initialization tracingLevel specified as a null string.
            {
                string tracingLevel = null;
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                Logger.Initialize(tracingLevel, false);
                bool isLogFileExpectedToExist = false;
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 7: Initialization tracingLevel specified as an empty string.
            {
                string tracingLevel = null;
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                Logger.Initialize(tracingLevel, false);
                bool isLogFileExpectedToExist = false;
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 8: Initialization tracingLevel specified as an invalid string.
            {
                string tracingLevel = "invalid";
                SourceLevels expectedTracingLevel = Logger.defaultTracingLevel;
                string expectedTraceSource = Logger.defaultTraceSource;
                Logger.Initialize(tracingLevel, false);
                bool isLogFileExpectedToExist = false;
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }

            //Test 9: Initialization with logfile set to empty string. 
            {
                SourceLevels expectedTracingLevel = SourceLevels.All;
                string expectedTraceSource = Logger.defaultTraceSource;
                string logFilePath = string.Empty;
                Logger.Initialize(traceSource: expectedTraceSource, logFilePath: logFilePath, tracingLevel:expectedTracingLevel);
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }
            //Test 10: Initialization with logfile set to null. 
            {
                SourceLevels expectedTracingLevel = SourceLevels.All;
                string expectedTraceSource = Logger.defaultTraceSource;
                string logFilePath = null;
                Logger.Initialize(traceSource: expectedTraceSource, logFilePath: logFilePath, tracingLevel: expectedTracingLevel);
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }
            //Test 11: Initialization with LogDirectory in Logger.GenerateLogFilePath set to empty string. 
            {
                SourceLevels expectedTracingLevel = SourceLevels.All;
                string expectedTraceSource = Logger.defaultTraceSource;
                string logFilePath = Logger.GenerateLogFilePath(Path.Combine(string.Empty, nameof(LoggerTestInitialization)));
                Logger.Initialize(traceSource: expectedTraceSource, logFilePath: logFilePath, tracingLevel: expectedTracingLevel);
                Assert.True(string.Compare(logFilePath, Logger.LogFileFullPath, ignoreCase: true) == 0, "The logfile should match the path specified");
                bool isLogFileExpectedToExist = ((uint)expectedTracingLevel >= (uint)SourceLevels.Information);
                TestLogger.VerifyInitialization(expectedTracingLevel, expectedTraceSource, Logger.LogFileFullPath, isLogFileExpectedToExist, testNo++);
                TestLogger.Cleanup(Logger.LogFileFullPath);
            }
            #endregion

        }

        /// <summary>
        /// Test to verify that there is no log file created if TracingLevel is set to off.
        /// </summary>
        [Test]
        public void LoggerTracingLevelOff()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Information,
                TracingLevel = SourceLevels.Off,
            };

            test.Initialize();
            test.Write();
            Assert.False(File.Exists(Logger.LogFileFullPath), $"Log file must not exist when tracing level is: {test.TracingLevel}");
            test.Verify(expectLogMessage: false); // The log message should be absent since the tracing level is set to Off.
            test.Cleanup();
        }

        /// <summary>
        /// Test to verify that the tracinglevel setting filters message logged at lower levels.
        /// Verifies that a test log entries logged at Information level are not present in log when tracingLevel
        /// is set to 'Critical'
        /// </summary>
        [Test]
        public void LoggerInformationalNotLoggedWithCriticalTracingLevel()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Information,
                TracingLevel = SourceLevels.Critical,
            };

            test.Initialize();
            test.Write();
            test.Verify(expectLogMessage:false); // The log message should be absent since the tracing level is set to collect messages only at 'Critical' logging level
            test.Cleanup();
        }

        /// <summary>
        /// Test to verify that WriteWithCallstack() method turns on the callstack logging
        /// </summary>
        [Test]
        public void LoggerWithCallstack()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Warning,
                TracingLevel = SourceLevels.Information,
            };

            test.Initialize();
            test.WriteWithCallstack();
            test.Verify(); // This should verify the logging of callstack fields as well.
            test.Cleanup();
        }

        /// <summary>
        /// Test to verify that callstack logging is turned on, it does not get logged because tracing level filters them out.
        /// </summary>
        [Test]
        public void LoggerWithCallstackFilteredOut()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Information,
                TracingLevel = SourceLevels.Error,
            };

            test.Initialize();
            test.WriteWithCallstack();
            test.Verify(expectLogMessage:false); // The log message and corresponding callstack details should be absent since the tracing level is set to collect messages only at 'Error' logging level
            test.Cleanup();
        }

        /// <summary>
        /// No TraceSource test to verify that WriteWithCallstack() method turns on the callstack logging
        /// </summary>
        [Test]
        public void LoggerNoTraceSourceWithCallstack()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Warning,
                TracingLevel = SourceLevels.Information,
                DoNotUseTraceSource = true,
            };

            test.Initialize();
            test.WriteWithCallstack();
            test.Verify(); // This should verify the logging of callstack fields as well.
            test.Cleanup();
        }

        /// <summary>
        /// No TraceSrouce test to verify that callstack logging is turned on, it does not get logged because tracing level filters them out.
        /// </summary>
        [Test]
        public void LoggerNoTraceSourceWithCallstackFilteredOut()
        {
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                EventType = TraceEventType.Information,
                TracingLevel = SourceLevels.Error,
                DoNotUseTraceSource = true,
            };

            test.Initialize();
            test.WriteWithCallstack();
            test.Verify(expectLogMessage: false); // The log message and corresponding callstack details should be absent since the tracing level is set to collect messages only at 'Error' logging level
            test.Cleanup();
        }

        /// <summary>
        /// Tests to verify that upon changing TracingLevel from Warning To Error, 
        /// after the change, messages of Error type are present in the log and those logged with warning type are not present.
        /// </summary>
        [Test]
        public void LoggerTracingLevelFromWarningToError()
        {
            // setup the test object
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
            };
            TestTracingLevelChangeFromWarningToError(test);
        }

        /// <summary>
        /// Tests to verify that upon changing TracingLevel from Error To Warning,  
        /// after the change, messages of Warning as well as of Error type are present in the log.
        /// </summary>
        [Test]
        public void LoggerTracingLevelFromErrorToWarning()
        {
            // setup the test object
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
            };
            TestTracingLevelChangeFromErrorToWarning(test);
        }

        /// <summary>
        /// When not use TraceSource, test to verify that upon changing TracingLevel from Warning To Error, 
        /// after the change, messages of Error type are present in the log and those logged with warning type are not present.
        /// </summary>
        [Test]
        public void LoggerNoTraceSourceTracingLevelFromWarningToError()
        {
            // setup the test object
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                DoNotUseTraceSource = true,
            };
            TestTracingLevelChangeFromWarningToError(test);
        }

        /// <summary>
        /// Tests out AutoFlush funcitonality. The verification is two fold.
        /// 1st is to verify that the setting is persistent. 
        /// 2nd that after a lot of log entries are written with AutoFlush on, explicitly flushing and closing the Tracing does not increase the file size
        ///     thereby verifying that there was no leftover log entries being left behind to be flushed.
        /// </summary>
        [Test]
        public void LoggerAutolFlush()
        {
            // setup the test object
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                AutoFlush = true,
                TracingLevel = SourceLevels.All
            };
            test.Initialize();
            // Write 10000 lines of log
            Parallel.For(0, 100, (i) => test.Write($"Message Number:{i}, Message:{test.LogMessage}"));
            long logContentsSizeBeforeExplicitFlush = (new FileInfo(test.LogFileName)).Length;
            // Please note that Logger.Close() first flushes the logs before closing them out.
            Logger.Flush();
            long logContentsSizeAfterExplicitFlush = (new FileInfo(test.LogFileName)).Length;
            Assert.True(logContentsSizeBeforeExplicitFlush == logContentsSizeAfterExplicitFlush, "The length of log file with autoflush before and after explicit flush must be same");
            test.Cleanup();
        }

        /// <summary>
        ///  When not use TraceSource, test to verify that upon changing TracingLevel from Error To Warning, 
        ///  after the change, messages of Warning as well as of Error type are present in the log.
        /// </summary>
        [Test]
        public void LoggerNoTraceSourceTracingLevelFromErrorToWarning()
        {
            // setup the test object
            TestLogger test = new TestLogger()
            {
                TraceSource = MethodInfo.GetCurrentMethod().Name,
                DoNotUseTraceSource = true,
            };
            TestTracingLevelChangeFromErrorToWarning(test);
        }

        private static void TestTracingLevelChangeFromWarningToError(TestLogger test)
        {
            test.Initialize();
            Logger.TracingLevel = SourceLevels.Warning;
            string oldMessage = @"Old Message with Tracing Level set to Warning";
            test.LogMessage = oldMessage;
            // Initially with TracingLevel at Warning, logging of Warning type does not get filtered out.
            Assert.AreEqual(SourceLevels.Warning, Logger.TracingLevel);
            {
                test.EventType = TraceEventType.Warning;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Warning, message: oldMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }
            // and logging of Error type also succeeeds
            {
                test.EventType = TraceEventType.Error;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Error, message: oldMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }

            //Now Update the tracing level to Error. Now logging both of Warning type gets filtered and only Error type should succeed.
            Logger.TracingLevel = SourceLevels.Error;
            Assert.AreEqual(SourceLevels.Error, Logger.TracingLevel);
            string newMessage = @"New Message After Tracing Level set to Error";
            test.LogMessage = newMessage;

            // Now with TracingLevel at Error, logging of Warning type gets filtered out.
            {
                test.EventType = TraceEventType.Warning;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Warning, message: newMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: false);
                });
            }
            // but logging of Error type succeeds
            {
                test.EventType = TraceEventType.Error;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Error, message: newMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }

            test.VerifyPending();
            test.Cleanup();
        }

        private static void TestTracingLevelChangeFromErrorToWarning(TestLogger test)
        {
            test.Initialize();
            Logger.TracingLevel = SourceLevels.Error;
            string oldMessage = @"Old Message with Tracing Level set to Error";
            test.LogMessage = oldMessage;
            // Initially with TracingLevel at Error, logging of Warning type gets filtered out.
            Assert.AreEqual(SourceLevels.Error, Logger.TracingLevel);
            {
                test.EventType = TraceEventType.Warning;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Warning, message: oldMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: false);
                });
            }
            // But logging of Error type succeeeds
            {
                test.EventType = TraceEventType.Error;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Error, message: oldMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }

            //Now Update the tracing level to Warning. Now logging both of Error type and Warning type should succeed.
            Logger.TracingLevel = SourceLevels.Warning;
            Assert.AreEqual(SourceLevels.Warning, Logger.TracingLevel);
            string newMessage = @"New Message After Tracing Level set to Warning";
            test.LogMessage = newMessage;

            // Now with TracingLevel at Warning, logging of Warning type does not get filtered out.
            {
                test.EventType = TraceEventType.Warning;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Warning, message: newMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }
            // and logging of Error type also succeeds
            {
                test.EventType = TraceEventType.Error;
                test.Write();
                test.PendingVerifications.Add(() =>
                {
                    test.Verify(eventType: TraceEventType.Error, message: newMessage, callstackMessage: null, shouldVerifyCallstack: false, expectLogMessage: true);
                });
            }

            test.VerifyPending();
            test.Cleanup();
        }
    }
}
