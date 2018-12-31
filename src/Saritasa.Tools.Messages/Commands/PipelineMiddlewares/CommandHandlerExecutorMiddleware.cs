﻿// Copyright (c) 2015-2018, Saritasa. All rights reserved.
// Licensed under the BSD license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Saritasa.Tools.Messages.Abstractions;
using Saritasa.Tools.Messages.Common;
using Saritasa.Tools.Messages.Internal;

namespace Saritasa.Tools.Messages.Commands.PipelineMiddlewares
{
    /// <summary>
    /// Default command executor. Rejected commands are not processed.
    /// </summary>
    public class CommandHandlerExecutorMiddleware : BaseHandlerExecutorMiddleware,
        IMessagePipelineMiddleware, IAsyncMessagePipelineMiddleware
    {
        /// <summary>
        /// Middleware identifier.
        /// </summary>
        public string Id { get; set; } = nameof(CommandHandlerExecutorMiddleware);

        /// <summary>
        /// Include execution duration into processing result.
        /// </summary>
        public bool IncludeExecutionDuration { get; set; } = true;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="throwExceptionsOnFail">If there were exception during processing it will be rethrown. Default is <c>true</c>.</param>
        public CommandHandlerExecutorMiddleware(bool throwExceptionsOnFail = true) : base(throwExceptionsOnFail)
        {
        }

        /// <inheritdoc />
        public virtual void Handle(IMessageContext messageContext)
        {
            var handlerMethod = messageContext.GetItemByKeyOrDefault<MethodInfo>(CommandHandlerLocatorMiddleware.HandlerMethodKey);
            var handler = messageContext.GetItemByKeyOrDefault(BaseHandlerResolverMiddleware.HandlerObjectKey);
            if (handlerMethod == null || handler == null)
            {
                throw new InvalidOperationException(
                    "Cannot find command handler method and/or command handler object in message context. " +
                           "Please provide \"handler-method\" and \"handler-object\" in message context items.");
            }

            // Invoke method and resolve parameters if needed.
            Stopwatch stopwatch = null;
            if (IncludeExecutionDuration)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }

            try
            {
                ExecuteHandler(handler, messageContext.Content, messageContext.ServiceProvider, handlerMethod);
                messageContext.Status = ProcessingStatus.Completed;
            }
            catch (TargetInvocationException ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(TargetInvocationException), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                if (ex.InnerException != null)
                {
                    messageContext.FailException = ex.InnerException;
                    CaptureException(messageContext);
                }
            }
            catch (TargetException ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(TargetException), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                if (ex.InnerException != null)
                {
                    messageContext.FailException = ex.InnerException;
                    CaptureException(messageContext);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(Exception), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                messageContext.FailException = ex;
                CaptureException(messageContext);
            }
            finally
            {
                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    messageContext.Items[MessageContextConstants.ExecutionDurationKey] = (int)stopwatch.ElapsedMilliseconds;
                }
            }
        }

        /*
         * There is code duplicate for HandleAsync method. We can make Handle method async instead.
         * But I did this for sake of performance - making method async would add ~9% decrease.
         */

        /// <inheritdoc />
        public virtual async Task HandleAsync(IMessageContext messageContext, CancellationToken cancellationToken)
        {
            var handlerMethod = messageContext.GetItemByKeyOrDefault<MethodInfo>(CommandHandlerLocatorMiddleware.HandlerMethodKey);
            var handler = messageContext.GetItemByKeyOrDefault(BaseHandlerResolverMiddleware.HandlerObjectKey);
            if (handlerMethod == null || handler == null)
            {
                throw new InvalidOperationException(
                    "Cannot find command handler method and/or command handler object in message context. " +
                           "Please provide \"handler-method\" and \"handler-object\" in message context items.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Invoke method and resolve parameters if needed.
            Stopwatch stopwatch = null;
            if (IncludeExecutionDuration)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }

            try
            {
                await ExecuteHandlerAsync(handler, messageContext.Content, messageContext.ServiceProvider, handlerMethod,
                    cancellationToken);
                messageContext.Status = ProcessingStatus.Completed;
            }
            catch (TargetInvocationException ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(TargetInvocationException), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                if (ex.InnerException != null)
                {
                    messageContext.FailException = ex.InnerException;
                    CaptureException(messageContext);
                }
            }
            catch (TargetException ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(TargetException), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                if (ex.InnerException != null)
                {
                    messageContext.FailException = ex.InnerException;
                    CaptureException(messageContext);
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Warn(string.Format(Properties.Strings.ExceptionWhileProcess,
                    nameof(Exception), handler, ex), nameof(CommandHandlerExecutorMiddleware));
                messageContext.Status = ProcessingStatus.Failed;
                messageContext.FailException = ex;
                CaptureException(messageContext);
            }
            finally
            {
                if (stopwatch != null)
                {
                    stopwatch.Stop();
                    messageContext.Items[MessageContextConstants.ExecutionDurationKey] = (int)stopwatch.ElapsedMilliseconds;
                }
            }
        }

        private void CaptureException(IMessageContext messageContext)
        {
            if (CaptureExceptionDispatchInfo)
            {
                messageContext.Items[MessageContextConstants.ExceptionDispatchInfoKey] =
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(messageContext.FailException);
            }
        }
    }
}
