// Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD, and Contributors.
// All rights reserved. Licensed under the BSD 3-Clause License; see License.txt.

using System;

namespace Moq.Tests
{
	public sealed class ConstantDefaultValueProvider : DefaultValueProvider
	{
		private readonly object value;

		public ConstantDefaultValueProvider(object value)
		{
			this.value = value;
		}

		protected internal override object GetDefaultValue(Type type, Mock mock)
		{
			return this.value;
		}
	}
}
