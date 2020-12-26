---
layout: post
title:  "MockSequence and mock.InSequence"
---

`MockSequence` is the little niche feature that led me to working on
Moq in the first place. I was playing around with the idea of a push-
based JSON parser that would invoke a callback for each token read from
the input. In my unit tests, I needed to ensure that the right sequence
of callbacks got invoked for a given JSON input string.

# What is it?

`MockSequence` can be used to check whether several calls in a single
mock are made in a specific order:

```csharp
public interface IJsonTokenSink
{
    void OnBoolean(bool value);
    void OnColon();
    void OnComma();
    void OnLeftAngleBracket();
    void OnLeftCurlyBrace();
    void OnNull();
    void OnNumber(string value);
    void OnRightAngleBracket();
    void OnRightCurlyBrace();
    void OnString(string value);
    void OnCompleted();
}

const string input = "[ 42, true ]";

var mock = new Mock<IJsonTokenSink>(MockBehavior.Strict);

var seq = new MockSequence();
mock.InSequence(seq).Setup(m => m.OnLeftAngleBracket());
mock.InSequence(seq).Setup(m => m.OnNumber("42"));
mock.InSequence(seq).Setup(m => m.OnComma());
mock.InSequence(seq).Setup(m => m.OnBoolean(true));
mock.InSequence(seq).Setup(m => m.OnRightAngleBracket());
mock.InSequence(seq).Setup(m => m.OnCompleted());

var tokenizer = new JsonTokenizer(tokenSink: mock.Object);
tokenizer.Tokenize(input);
```

# Why is it problematic?

Unfortunately, `MockSequence` as it is today is only of limited use,
and extending it to something more powerful may turn out to be a bigger
effort than restarting from scratch.

For one thing, there's no way to ensure that _all_ configured calls
in the sequence have been made. You can guard against superfluous calls
using `MockBehavior.Strict`; but you cannot easily detect missing ones
because `mock.Verify[All]` won't work here, since mock sequences are
using conditional setups under the hood (and those get excluded from
verification).

Another problem is that sequences cannot easily span across several
mocks. It's possible to call `.InSequence(seq)` on different mocks, but
the problem is that noone will see the complete invocation sequence;
each mock will only see its own invocations in the sequence.

Like I mentioned in [moq/moq4#75](https://github.com/moq/moq4/issues/75),
those issues with `MockSequence` could perhaps be solved by recording
invocations on the sequence object itself, and by adding `.Verify[All]`
methods to it, as well.

Because of the way how `MockSequence` uses conditional setups in its
implementation, it isn't thread-safe. Sequence setups were once based
on conditional setups, too, and they also weren't thread-safe. This was
changed in [moq/moq4#6281f4e](https://github.com/moq/moq4/commit/6281f4e1c69eef7e8d24e43db910ff670e707d37). Unfortunately, the same kind
of change is not possible for `MockSequence`. I'll hint at the reason
for this towards the end of this post.

Finally (but that is a very minor nit), it can be potentially confusing
to Moq newcomers that there are two different kinds of "sequences":

 * "setup sequences" aka `mock.InSequence(...)`
 * "sequence setups" aka `mock.SetupSequence(...)`

# Replacement

The example given above could be rewritten without `MockSequence` as
follows:

```csharp
var seq = 0;
mock.When(() => seq == 0).Setup(m => m.OnLeftAngleBracket()) .Callback(() => seq++);
mock.When(() => seq == 1).Setup(m => m.OnNumber("42"))       .Callback(() => seq++);
mock.When(() => seq == 2).Setup(m => m.OnColon())            .Callback(() => seq++);
mock.When(() => seq == 3).Setup(m => m.OnBoolean(true))      .Callback(() => seq++);
mock.When(() => seq == 4).Setup(m => m.OnRightAngleBracket()).Callback(() => seq++);
mock.When(() => seq == 5).Setup(m => m.OnCompleted())        .Callback(() => seq++);
```

Quite a bit of repetition&mdash;`MockSequence` did help there!&mdash;
but perfectly feasible, so I'll proceed with its removal.

(Incidentally, this rewrite uncovers the reason why sequences aren't
thread-safe: the comparison test on `seq` in `.When` and the update of
`seq` in `.Callback` do not happen as an atomic operation; so it's
possible that one of these setups will trigger twice and the succeeding
one might get skipped.)

# Removal

Removing `MockSequence` is straightforward (see commit [`958a982`](https://github.com/stakx/dissecting-moq4/commit/958a982025a147a2c66f7130e0e7009258d487af)) because
it is really just a slim layer on top of conditional setups.
