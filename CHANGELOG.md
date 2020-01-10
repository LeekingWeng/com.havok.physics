# Changelog
All notable changes to this project will be documented in this file.

## [0.1.2-preview] - 9999-12-31

### Changed

### Fixed
- Fixed the issue of uninitialized array when scheduling collision event jobs with no dynamic bodies in the scene.
- Fixed an issue where contacts were not being correctly disabled in an IContactsJob.
- The Havok Visual Debugger (VDB) is now always stepped, even when there are no dynamic bodies in the scene.
- Fixed the job handle ordering during step. This fixes errors when simulation callbacks were added.
- The VDB is now correctly initialised with the port supplied.

## [0.1.1-preview] - 2019-09-20

- First public release

### Changed
- The plugins are now free to use for everyone until January 15th 2020

### Fixed
- Fixed potential IndexOutOfRangeException when executing IBodyPairsJob, IContactsJob or IJacobiansJob

## [0.1.0-preview.2] - 2019-09-05

### Fixed
- Bodies tagged for contact welding now actually get welding applied

## [0.1.0-preview.1] - 2019-08-29

- First pre-release package version
