---
layout: post
title:  "mock.SetReturnsDefault"
date: 2020-12-26 18:22:57
---

Let's start with something simple: the [`mock.SetReturnsDefault` method](https://github.com/stakx/dissecting-moq4/blob/341ec34c623fb0aba6572aa64e37104f4da2662d/src/Moq/Mock.cs#L790).
I'll quickly describe this feature before stating my reasons for wanting
to remove it.

# What is it?

`SetReturnsDefault` is an easy way to set up default return values for
loose mocks (i.e. when `mock.Behavior == MockBehavior.Loose`). You pass
it a type, and a value of that type, and Moq will use that value as the
default return value whenever an unexpected invocation of a method with
that return type occurs:

```csharp
public interface IPerson
{
    string Name { get; }
}

var mock = new Mock<IPerson>();
mock.SetReturnsDefault<string>("Alice");

Assert.Equal("Alice", mock.Object.Name);
```

This is a very coarse-grained mechanism, as it applies to _all_ methods
with the same return type. Assume you have this interface instead:

```csharp
public interface IPerson
{
    string FirstName { get; }
    string LastName { get; }
}
```

`SetReturnsDefault` won't be of much use here if you want different
default return values for `FirstName` and `LastName`; you'll need setups
to target the properties individually.

# Reasons for removal

There are a few reasons why I want to remove this method:

 1. In Visual Studio IntelliSense, it gets in the way of the much more
    valuable `Setup...` methods due to the identical name prefix (`Set`).
    This is at times annoying.

 2. More importantly, Moq already contains a much more powerful facility
    for producing default values: `DefaultValueProvider`. Anything you
    can do with `SetReturnsDefault`, you can do with those providers.

Default value providers certainly aren't perfect at this time; I'll
try to write a little about that in a later post.

# Removal

Removing this method is quite straightforward (see the commits
[`df8df3a`](https://github.com/stakx/dissecting-moq4/commit/df8df3af7b06130d9ba8c3aa1e080653218be7b0) and [`000d488`](https://github.com/stakx/dissecting-moq4/commit/000d488fb5e4d41e13593e7ecb7eb6d6dd142584)).
As can be seen in the test project, `SetReturnsDefault` can indeed be
replaced with a custom default value provider (as claimed above).

We can also get rid of the `Mock.ConfiguredDefaultValues` property,
meaning _all_ mocks will get a little lighter.
