---
layout: post
title:  "default(MockBehavior) == MockBehavior.Default ?"
date: 2020-12-27 16:56:49
---

I'll finish today's posts with a quick one:

```csharp
Assert.Equal(default(MockBehavior), MockBehavior.Default);
```

I hope you've never run that test, because you'd be surprised to learn
that it fails! This comes down to how `MockBehavior` is defined (sans
documentation comments):

```csharp
public enum MockBehavior
{
	Strict,
	Loose,
	Default = Loose,
}
```

The problem here is that the default value is mentioned last, instead of
first. It should really have been:

```csharp
public enum MockBehavior
{
	Default,
	Loose = Default,
	Strict,
}
```

Whether a `Default` enum value is needed at all (given that you can
write `default(MockBehavior)`) is up for debate, but let's leave it in
since the [.NET Framework Design Guidelines for enums](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/enum) have this to say on the topic:

> ✔️ DO provide a value of zero on simple enums.
>
> Consider calling the value something like "None." If such a value is
> not appropriate for this particular enum, the most common default
> value for the enum should be assigned the underlying value of zero.

I've fixed this minor design mistake in [`fdc644f`](https://github.com/stakx/dissecting-moq4/commit/fdc644f72894f63e395ea7f7fe13acb60ffca4c0).

# Why wasn't this fixed long ago?

This turns out to be a mix between [behavior and binary breaking change](https://docs.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes#behavior-breaking-change).
If you dropped an Moq assembly thus updated in an existing project
without recompiling the latter, its loose mocks would suddenly act like
strict ones, and vice versa. (The reason for that being that IL refers
not to the enum fields, but to their numeric values; that is, enums get
embedded in IL as integer constants.)

Such changes are not a big issue when you're using Moq directly, and
you can simply recompile your test project. However, when you use Moq
through a third-party library, you can't just recompile those; making
such a change in Moq forces those libraries' maintainers to follow suit
and release an updated version themselves.
