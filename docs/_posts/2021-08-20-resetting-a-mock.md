---
layout: post
title:  "Resetting a mock"
---

Mocks, or just their recorded invocations, can be reset.

## How does it work?

Moq records all invocations that happen on a mock. This record can be
cleared using `mock.Invocations.Clear()` (or using the obsolete method
`mock.ResetCalls()`). This will also "unmatch" setups that have already
matched by one or more of the recorded calls.

A mock can be completely reset via `mock.Reset()`. This will remove not
just the recorded invocations, but also all setups and event handlers.

## Why does this feature exist?

I suspect there are two reasons why this feature exists:

 1. Recording all invocations could be problematic in cases where a mock
    gets invoked _a lot_. All invocations get logged (for later
    verification), and that uses up memory. If verification isn't going
    to be needed, that memory is essentially wasted. So sometimes it may
    be necessary to periodically clear the invocation log so one won't
    run into a low memory situation.

 2. Mock reuse is desired. Create a mock, set it up, use it; then reset
    it and use it for something else. This saves one the trouble of
    creating and setting up two mocks.

## What's wrong with it?

I think that there are potentially better solutions for both cases than
the ability to reset a mock.

In the case of (1), it would be better if Moq allowed one to opt-out of
recording all invocations, e.g. through an additional `MockBehavior`
flag:

```csharp
new Mock<Foo>(MockBehavior.Loose | MockBehavior.DontRecordInvocations)
```

Maybe this would end up somewhat differently, but you get the idea. By
being able to disable the invocation log, one wouldn't be in danger of
low memory situations due to unneeded invocations sticking around.

With regard to (2), creating another mock and setting it up shouldn't be
a big hassle. One can always extract the creation and setting up of a
fresh mock into a function that can be called as many times as needed.

# Removal

Removing this feature was fairly easy; I've done so in [f7a9c0c](https://github.com/stakx/dissecting-moq4/commit/f7a9c0c0c02c4cbd575dd99c82471130618e841c) (approx. 250 lines of code removed).

I also like the fact that we're getting rid of two methods in
`SetupCollection`: `Clear` and `Reset`. It probably wasn't very obvious
why two similar methods were needed. (The former would remove setups;
the latter would only "unmatch" them.)
