---
layout: post
title:  "setup.ReturnsAsync and friends"
---

Another part of Moq that is obsolete for the most part is the async
setup helper verbs `setup.ReturnsAsync` and `setup.ThrowsAsync`.

# What are they?

These extension methods allow you to set up the result of an async
method call without having to manually wrap the desired result in a
task:

```diff
-mock.Setup(m => m.CountGoldPiecesAsync()).Returns     (Task.FromResult(11873));
+mock.Setup(m => m.CountGoldPiecesAsync()).ReturnsAsync(                11873 );
```

```diff
-mock.Setup(m => m.GoPiratingAsync()).Throws     (Task.FromException(new PirateException("arr!")));
+mock.Setup(m => m.GoPiratingAsync()).ThrowsAsync(                   new PirateException("arr!") );
```

# You could let the compiler do most the work...

Given C# `async`/`await`, the same work could be done by the compiler:

```diff
-mock.Setup(m => m.CountGoldPiecesAsync()).Returns(Task.FromResult(11873));
+mock.Setup(m => m.CountGoldPiecesAsync()).Returns(    async () => 11873 );
```

```diff
-mock.Setup(m => m.GoPiratingAsync()).Throws(Task.FromException(new PirateException("arr!")));
+mock.Setup(m => m.GoPiratingAsync()).Throws( async () => throw new PirateException("arr!") );
```

Though the compiler will warn that the lambdas aren't `await`-ing
anything and will therefore run synchronously, which can be a little
annoying.

# Alternative API design in Moq 4.16

In version 4.16 (which is actually not published at this time of
writing), Moq has gained an alternate API to do the same thing in yet
another way&mdash;it allows you do set up the `.Result` of tasks
directly:

```diff
-mock.Setup(m => m.CountGoldPiecesAsync()       ).Returns(Task.FromResult(11873));
+mock.Setup(m => m.CountGoldPiecesAsync().Result).Returns(                11873 );
```

```diff
-mock.Setup(m => m.GoPiratingAsync()       ).Throws(Task.FromException(new PirateException("arr!")));
+mock.Setup(m => m.GoPiratingAsync().Result).Throws(                   new PirateException("arr!") );
```

This was added in [moq/moq4#1126](https://github.com/moq/moq4/pull/1126)
and was surprisingly easy to implement... check it out!

It has a few benefits even over a native C# solution, among them the
ability to do recursive setups (method chaining) across async calls,
and no compiler warnings due to missing `await`s.

But it's entirely possible that not everyone will like this new feature;
I do, so I'm going ahead here with removing the async setup verbs!

# Removal

This is another feature easily removed, which isn't surprising at all,
since the code parts in question are only extension methods. I've done
this in [a111c23](https://github.com/stakx/dissecting-moq4/commit/a111c2329e77ecb0a4229b5c5afbd0251fbad798).

The `setup.ReturnsAsync` and `setup.ThrowsAsync` extension methods make
up much more code (when including tests) than Moq 4.16's alternative
solution: approx. 2,400 lines of code vs. only approx. 600 for the
code required to enable setting up `task.Result`!
