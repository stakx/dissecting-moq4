// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD, and Contributors.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Moq.Language;

namespace Moq
{
	/// <summary>
	/// Helper for sequencing return values in the same method.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static partial class SequenceExtensions
	{
		/// <summary>
		/// Return a sequence of values, once per call.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Please use instance method Mock<T>.SetupSequence instead.")]
		[SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By Design")]
		public static ISetupSequentialResult<TResult> SetupSequence<TMock, TResult>(
			this Mock<TMock> mock,
			Expression<Func<TMock, TResult>> expression)
			where TMock : class
		{
			return mock.SetupSequence(expression);
		}

		/// <summary>
		/// Performs a sequence of actions, one per call.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Please use instance method Mock<T>.SetupSequence instead.")]
		[SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures", Justification = "By Design")]
		public static ISetupSequentialAction SetupSequence<TMock>(this Mock<TMock> mock, Expression<Action<TMock>> expression)
			where TMock : class
		{
			return mock.SetupSequence(expression);
		}
	}
}
