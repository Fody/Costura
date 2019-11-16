# Contributing

Thanks you for your interest in contributing - Please see our our [Code of Conduct](CODE_OF_CONDUCT.md).


### Bug Fixes

If you're looking for something to fix, please browse the open issues.

Follow the style used by the [.NET Foundation](https://github.com/dotnet/corefx/blob/master/Documentation/coding-guidelines/coding-style.md), with a few exceptions:

- Apply readonly on class level private variables that are assigned in the constructor
- 4 SPACES - tabs do not exist :)

Read and follow our [Pull Request template](PULL_REQUEST_TEMPLATE.md) if you are submitting fixes.

### Feature Requests

To propose a change or new feature, please make use the feature request area in issues.

#### Non-Starter Topics
The following topics should generally not be proposed for discussion as they are non-starters:

* Large renames of APIs
* Large non-backward-compatible breaking changes
* Avoid clutter posts like "+1" which do not serve to further the conversation

#### Proposal States
##### Open
Open proposals are still under discussion. Please leave your concrete, constructive feedback on this proposal. +1s and other clutter posts which do not add to the discussion will be removed.

##### Accepted
Accepted proposals are proposals that both the community and core team agree should be a part of projects. These proposals are ready for implementation. These proposals are available for anyone to work on unless it is already assigned to someone.

If you wish to start working on an accepted proposal, please reply to the thread so we can mark you as the implementor and change the title to In Progress. This helps to avoid multiple people working on the same thing. If you decide to work on this proposal publicly, feel free to post a link to the branch as well for folks to follow along.

###### What "Accepted" does mean
* Any community member is welcome to work on the idea.
* The maintainers _may_ consider working on this idea on their own, but has not done so until it is marked "In Progress" with a team member assigned as the implementor.
* Any pull request implementing the proposal will be welcomed with an API and code review.

###### What "Accepted" does not mean
* The proposal will ever be implemented, either by a community member or maintainers.
* The maintainers are committing to implementing a proposal, even if nobody else does. 

##### In Progress
Once a developer has begun work on a proposal, either from the team or a community member, the proposal is marked as in progress with the implementors name and (possibly) a link to a development branch to follow along with progress.

#### Rejected
Rejected proposals will not be implemented or merged into the code base. Once a proposal is rejected, the thread will be closed and the conversation is considered completed, pending considerable new information or changes..