---
layout: page
title: About
permalink: /about/
---

During the past few years, I have done quite a bit of work on the .NET
mocking library [Moq](https://github.com/moq); more specifically, on
version 4, which lives in the GitHub repository [moq/moq4](https://github.com/moq/moq4).

Moq 4 was already a mature library and in widespread use when I started
working on it, so I tried to always keep backwards compatibility in mind
and not make any unnecessary breaking changes. That being said, I've
often been tempted to simplify existing code... even when I knew I
couldn't.

In order to see just how far the code base _could_ be cleaned up and
simplified, I've started a disconnected fork of Moq 4 at
[stakx/dissecting-moq4](https://github.com/stakx/dissecting-moq4) (this
project), with the idea of removing existing code bit by bit, feature
by feature, until only some core features remain.

This site documents the code work done in that fork. I'll try to
describe the code bits that I'm removing as I proceed, in the hope that
this might shed some light on...

 * how the various bits of Moq 4's internals fit together. This could be
   interesting if you want to understand the code base better;

 * parts of Moq 4 that could have been done better. Seeing the code that
   makes up a feature in isolation may be a good way of recognizing
   simpler alternative designs.

I have no idea where exactly, and how far, I'll take this. See the
other posts on this site to find out!