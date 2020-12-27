// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD, and Contributors.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Moq.Behaviors;
using Moq.Properties;

namespace Moq
{
	internal sealed partial class MethodCall : SetupWithOutParameterSupport
	{
		private LimitInvocationCount limitInvocationCount;
		private Behavior callback;
		private Behavior raiseEvent;
		private Behavior returnOrThrow;
		private Behavior afterReturnCallback;
		private Func<bool> condition;
		private string failMessage;

		private string declarationSite;

		public MethodCall(Expression originalExpression, Mock mock, Func<bool> condition, InvocationShape expectation)
			: base(originalExpression, mock, expectation)
		{
			this.condition = condition;

			if ((mock.Switches & Switches.CollectDiagnosticFileInfoForSetups) != 0)
			{
				this.declarationSite = GetUserCodeCallSite();
			}
		}

		public string FailMessage
		{
			get => this.failMessage;
		}

		public override Func<bool> Condition => this.condition;

		private static string GetUserCodeCallSite()
		{
			try
			{
				var thisMethod = MethodBase.GetCurrentMethod();
				var mockAssembly = Assembly.GetExecutingAssembly();
				var frame = new StackTrace(true)
					.GetFrames()
					.SkipWhile(f => f.GetMethod() != thisMethod)
					.SkipWhile(f => f.GetMethod().DeclaringType == null || f.GetMethod().DeclaringType.Assembly == mockAssembly)
					.FirstOrDefault();
				var member = frame?.GetMethod();
				if (member != null)
				{
					var declaredAt = new StringBuilder();
					declaredAt.AppendNameOf(member.DeclaringType).Append('.').AppendNameOf(member, false);
					var fileName = Path.GetFileName(frame.GetFileName());
					if (fileName != null)
					{
						declaredAt.Append(" in ").Append(fileName);
						var lineNumber = frame.GetFileLineNumber();
						if (lineNumber != 0)
						{
							declaredAt.Append(": line ").Append(lineNumber);
						}
					}
					return declaredAt.ToString();
				}
			}
			catch
			{
				// Must NEVER fail, as this is a nice-to-have feature only.
			}

			return null;
		}

		protected override void ExecuteCore(Invocation invocation)
		{
			this.limitInvocationCount?.Execute(invocation);

			this.callback?.Execute(invocation);

			this.raiseEvent?.Execute(invocation);

			if (this.returnOrThrow != null)
			{
				this.returnOrThrow.Execute(invocation);
			}
			else if (invocation.Method.ReturnType != typeof(void))
			{
				if (this.Mock.Behavior == MockBehavior.Strict)
				{
					throw MockException.ReturnValueRequired(invocation);
				}
				else
				{
					new ReturnBaseOrDefaultValue(this.Mock).Execute(invocation);
				}
			}
			else
			{
				HandleEventSubscription.Handle(invocation, this.Mock);  // no-op for everything other than event accessors
			}

			this.afterReturnCallback?.Execute(invocation);
		}

		public override bool TryGetReturnValue(out object returnValue)
		{
			if (this.returnOrThrow is ReturnValue rv)
			{
				returnValue = rv.Value;
				return true;
			}
			else
			{
				returnValue = default;
				return false;
			}
		}

		public void SetCallBaseBehavior()
		{
			if (this.Mock.MockedType.IsDelegateType())
			{
				throw new NotSupportedException(Resources.CallBaseCannotBeUsedWithDelegateMocks);
			}

			this.returnOrThrow = ReturnBase.Instance;
		}

		public void SetCallbackBehavior(Delegate callback)
		{
			if (callback == null)
			{
				throw new ArgumentNullException(nameof(callback));
			}

			ref Behavior behavior = ref (this.returnOrThrow == null) ? ref this.callback
			                                                         : ref this.afterReturnCallback;

			if (callback is Action callbackWithoutArguments)
			{
				behavior = new Callback(_ => callbackWithoutArguments());
			}
			else if (callback.GetType() == typeof(Action<IInvocation>))
			{
				// NOTE: Do NOT rewrite the above condition as `callback is Action<IInvocation>`,
				// because this will also yield true if `callback` is a `Action<object>` and thus
				// break existing uses of `(object arg) => ...` callbacks!
				behavior = new Callback((Action<IInvocation>)callback);
			}
			else
			{
				var expectedParamTypes = this.Method.GetParameterTypes();
				if (!callback.CompareParameterTypesTo(expectedParamTypes))
				{
					throw new ArgumentException(
						string.Format(
							CultureInfo.CurrentCulture,
							Resources.InvalidCallbackParameterMismatch,
							this.Method.GetParameterTypeList(),
							callback.GetMethodInfo().GetParameterTypeList()));
				}

				if (callback.GetMethodInfo().ReturnType != typeof(void))
				{
					throw new ArgumentException(Resources.InvalidCallbackNotADelegateWithReturnTypeVoid, nameof(callback));
				}

				behavior = new Callback(invocation => callback.InvokePreserveStack(invocation.Arguments));
			}
		}

		public void SetFailMessage(string failMessage)
		{
			this.failMessage = failMessage;
		}

		public void SetRaiseEventBehavior<TMock>(Action<TMock> eventExpression, Delegate func)
			where TMock : class
		{
			Guard.NotNull(eventExpression, nameof(eventExpression));

			var expression = ExpressionReconstructor.Instance.ReconstructExpression(eventExpression, this.Mock.ConstructorArguments);

			// TODO: validate that expression is for event subscription or unsubscription

			this.raiseEvent = new RaiseEvent(this.Mock, expression, func, null);
		}

		public void SetRaiseEventBehavior<TMock>(Action<TMock> eventExpression, params object[] args)
			where TMock : class
		{
			Guard.NotNull(eventExpression, nameof(eventExpression));

			var expression = ExpressionReconstructor.Instance.ReconstructExpression(eventExpression, this.Mock.ConstructorArguments);

			// TODO: validate that expression is for event subscription or unsubscription

			this.raiseEvent = new RaiseEvent(this.Mock, expression, null, args);
		}

		public void SetReturnValueBehavior(object value)
		{
			Debug.Assert(this.Method.ReturnType != typeof(void));
			Debug.Assert(this.returnOrThrow == null);

			this.returnOrThrow = new ReturnValue(value);
		}

		public void SetReturnComputedValueBehavior(Delegate valueFactory)
		{
			Debug.Assert(this.Method.ReturnType != typeof(void));
			Debug.Assert(this.returnOrThrow == null);

			if (valueFactory == null)
			{
				// A `null` reference (instead of a valid delegate) is interpreted as the actual return value.
				// This is necessary because the compiler might have picked the unexpected overload for calls
				// like `Returns(null)`, or the user might have picked an overload like `Returns<T>(null)`,
				// and instead of in `Returns(TResult)`, we ended up in `Returns(Delegate)` or `Returns(Func)`,
				// which likely isn't what the user intended.
				// So here we do what we would've done in `Returns(TResult)`:
				this.returnOrThrow = new ReturnValue(this.Method.ReturnType.GetDefaultValue());
			}
			else if (this.Method.ReturnType == typeof(Delegate))
			{
				// If `TResult` is `Delegate`, that is someone is setting up the return value of a method
				// that returns a `Delegate`, then we have arrived here because C# picked the wrong overload:
				// We don't want to invoke the passed delegate to get a return value; the passed delegate
				// already is the return value.
				this.returnOrThrow = new ReturnValue(valueFactory);
			}
			else if (IsInvocationFunc(valueFactory))
			{
				this.returnOrThrow = new ReturnComputedValue(invocation => valueFactory.DynamicInvoke(invocation));
			}
			else
			{
				ValidateCallback(valueFactory);

				if (valueFactory.CompareParameterTypesTo(Type.EmptyTypes))
				{
					// we need this for the user to be able to use parameterless methods
					this.returnOrThrow = new ReturnComputedValue(invocation => valueFactory.InvokePreserveStack());
				}
				else
				{
					this.returnOrThrow = new ReturnComputedValue(invocation => valueFactory.InvokePreserveStack(invocation.Arguments));
				}
			}

			bool IsInvocationFunc(Delegate callback)
			{
				var type = callback.GetType();
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<,>))
				{
					var typeArguments = type.GetGenericArguments();
					return typeArguments[0] == typeof(IInvocation)
						&& (typeArguments[1] == typeof(object) || this.Method.ReturnType.IsAssignableFrom(typeArguments[1]));
				}

				return false;
			}

			void ValidateCallback(Delegate callback)
			{
				var callbackMethod = callback.GetMethodInfo();

				// validate number of parameters:

				var numberOfActualParameters = callbackMethod.GetParameters().Length;
				if (callbackMethod.IsStatic)
				{
					if (callbackMethod.IsExtensionMethod() || callback.Target != null)
					{
						numberOfActualParameters--;
					}
				}

				if (numberOfActualParameters > 0)
				{
					var numberOfExpectedParameters = this.Method.GetParameters().Length;
					if (numberOfActualParameters != numberOfExpectedParameters)
					{
						throw new ArgumentException(
							string.Format(
								CultureInfo.CurrentCulture,
								Resources.InvalidCallbackParameterCountMismatch,
								numberOfExpectedParameters,
								numberOfActualParameters));
					}
				}

				// validate return type:

				var actualReturnType = callbackMethod.ReturnType;

				if (actualReturnType == typeof(void))
				{
					throw new ArgumentException(Resources.InvalidReturnsCallbackNotADelegateWithReturnType);
				}

				var expectedReturnType = this.Method.ReturnType;

				if (!expectedReturnType.IsAssignableFrom(actualReturnType))
				{
					// TODO: If the return type is a matcher, does the callback's return type need to be matched against it?
					if (typeof(ITypeMatcher).IsAssignableFrom(expectedReturnType) == false)
					{
						throw new ArgumentException(
							string.Format(
								CultureInfo.CurrentCulture,
								Resources.InvalidCallbackReturnTypeMismatch,
								expectedReturnType,
								actualReturnType));
					}
				}
			}
		}

		public void SetThrowExceptionBehavior(Exception exception)
		{
			this.returnOrThrow = new ThrowException(exception);
		}

		protected override void ResetCore()
		{
			this.limitInvocationCount?.Reset();
		}

		public void AtMost(int count)
		{
			this.limitInvocationCount = new LimitInvocationCount(this, count);
		}

		public override string ToString()
		{
			var message = new StringBuilder();

			if (this.failMessage != null)
			{
				message.Append(this.failMessage).Append(": ");
			}

			message.Append(base.ToString());

			if (this.declarationSite != null)
			{
				message.Append(" (").Append(this.declarationSite).Append(')');
			}

			return message.ToString().Trim();
		}
	}
}
