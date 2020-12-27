---
layout: post
title:  "Refactoring conditional setups"
date: 2020-12-27 14:59:44
---

I noticed in [the previous post]({% post_url 2020-12-26-mock-sequence %}) that neither `MockSequence` nor an earlier
version of `mock.SetupSequence` were thread-safe, due to the way how
they were implemented using conditional setups.

Let's start by quickly reviewing conditional setups.

# What are they?

Conditional setups are setups that only get matched when some external
condition is met. You set them up using `mock.When`:

```csharp
var env = "quux";

var mock = new Mock<ISomeInterface>();
mock.When(() => env == "foo").Setup(m => m.SomeMethod()).Callback(() => DoFoo());
mock.When(() => env == "bar").Setup(m => m.SomeMethod()).Callback(() => DoBar());

env = "bar";
mock.Object.SomeMethod();  // second setup will match

env = "foo";
mock.Object.SomeMethod();  // first setup will match
```

(I'm using the variable name `env`, short for "environment", to hint at
the fact that setup conditions always test state that lives outside of
the setups.)

Conditional setups are how all things "sequence" used to be implemented.
You'd typically have an integer counter that keeps track of how far you
have advanced the sequence. Each time a setup in the sequence gets
matched, you'd have to increase that counter.

# Simplifying the implementation behind conditional setups

The condition passed to `mock.When` ends up being wrapped internally by
the `Condition` class. Each `Setup` can have a `Condition` instance
associated with it.

`Condition` does not wrap _just_ that predicate; it also supports a
`success` delegate that gets invoked whenever the associated setup has
matched an invocation and is about to be executed. This delegate was
where the sequence counter used to be increased for `MockSequence`s.

It turns out that this was the _only_ case where `success` was used.
Let's verify this by removing it and recompiling to see what breaks:

```diff
 internal sealed class Condition
 {
     ...
 
-    public Condition(Func<bool> condition, Action success = null)
+    public Condition(Func<bool> condition)
     {
         this.condition = condition;
-        this.success = success;
+        this.success = null;
     }
 
     ...
 }
```

And it turns out that _nothing_ breaks, meaning that `success` is no
longer used anywhere. This class has essentially become obsolete, and
it can be replaced with the `Func<bool>` that it contains.

I've done this refactoring in [`2ba2fd9`](https://github.com/stakx/dissecting-moq4/commit/2ba2fd9f049d7c9e3a441e9a638270723521a826).

# Why does it matter?

Apart from making setups a little lighter (one less object reference),
this refactoring also removes an potential place from which potential
thread-safety issues can arise. No longer having the option to increase
a sequence counter in `Condition` means we will need to look for another
(and hopefully better) solution if and when we want to reintroduce
sequences!
