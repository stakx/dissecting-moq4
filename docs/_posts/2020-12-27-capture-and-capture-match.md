---
layout: post
title:  "Capture and CaptureMatch"
date: 2020-12-27 16:26:00
---

[Last time]({% post_url 2020-12-27-refactoring-conditional-setups %})
I removed some code that was problematic with regard to thread safety.
While refactoring, I also [touched a few lines in the `Setup.Execute`
method](https://github.com/stakx/dissecting-moq4/commit/2ba2fd9f049d7c9e3a441e9a638270723521a826#diff-6572ff90d645533ad93054f4e75e64d1983cca36f7c5ca36204b2edfbf4a3136L64-R65):

```diff
+// update matchers (important for `Capture`):
-// update condition (important for `MockSequence`) and matchers (important for `Capture`):
-this.Condition?.SetupEvaluatedSuccessfully();
 this.expectation.SetupEvaluatedSuccessfully(invocation);
```

Two things bother me here:

 * One `SetupEvaluatedSucessfully` calls gets removed, but one remains.
   Given that both methods are named identically, do they also have the
   same function? Does that mean that another thread-safety issue has
   remained unresolved?

 * The comment mentions argument matchers being updated. That seems
   wrong. Argument matchers should not have any side effects!

So let's take a closer look at the mentioned `Capture` class.

# What is it?

`Capture` is a static class like `It` that contains argument matcher
methods:

 * `Capture.In(collection)` acts like `It.IsAny<T>()`, but additionally
   it will add the inspected argument value to a given `collection` if
   the setup matches an invocation:

   ```csharp
   var values = new List<string>();
   mock.Setup(m => m.OnNumber(Capture.In(values)));
   mock.Setup(m => m.OnString(Capture.In(values)));

   mock.Object.OnNumber("42");
   mock.Object.OnString("Alice");

   Assert.Equal(new[] { "42", "Alice" }, values);
   ```

 * `Capture.With(captureMatch)` follows the same idea, but offers more
   flexibility: user code can specify what should happen with argument
   values (instead of just defaulting to adding to a collection), and
   optionally when argument values match (instead of defaulting to
   `It.IsAny<T>()` behavior):

   ```csharp
   var n = 0;
   var logA = new CaptureMatch<string>(
           arg => Console.WriteLine(arg),
           arg => arg.StartsWith("A"));
   mock.Setup(m => m.OnString(Capture.With(logA))).Callback(() => n++);

   mock.Object.OnString("Alice");  // should print Alice
   mock.Object.OnString("Bob");    // not mached

   Assert.Equal(1, n);
   ```

# What's wrong with it?

Conceptually, matchers are simple predicates: they take a value, run a
test on it, and report the test result (a truth value, i.e. `true` or
`false`). They shouldn't need to mutate state and thus cause visible
side effects in order to do this.

What's worse here is that extra machinery is needed for `Capture`: it
needs to be able to only update state (like an external collection) when
the setup that makes use of it is actually matched.

In that respect, `CaptureMatch` is very similar to the `Condition` class
we looked at in the last post: it keeps a test function, and something
like a `success` delegate, which needs to be invoked once the setup
is matched. So, just as with conditional setups, there must be a thread-
safety issue lurking around here.

# Alternatives

Code using `Capture.In` can be rewritten using `It.IsAny<>()` and
code in `.Callback` to add the value to a collection:

```csharp
values = new List<string>();
mock.Setup(m => m.OnNumber(It.IsAny<string>())).Callback(values.Add);
mock.Setup(m => m.OnString(It.IsAny<string>())).Callback(values.Add);
```

`CaptureMatch` can be similarly rewritten using `It.Is<>(condition)`
and any code in `.Callback`.

# Removal

The two classes are easily removed; see [`5b15506`](https://github.com/stakx/dissecting-moq4/commit/5b155061ad9d5e21a55ae374c2cb251e73924d81). The more satisfying
part here is removing all supporting internal machinery, i.e.
`SetupSuccessfullyEvaluated` ([`b549261`](https://github.com/stakx/dissecting-moq4/commit/b549261dffb98af05406f69f8a6f8de373462adf)): that method has sent its
tendrils into quite a few places in the code base, even verification.
(Regarding that last one, see [moq/moq4#968](https://github.com/moq/moq4/issues/968)
for an ingenious previous use case for `Capture.In`.)