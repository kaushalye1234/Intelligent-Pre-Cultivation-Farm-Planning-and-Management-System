# Farmer Registration Input Validation Design

Prevent invalid full-name and phone characters in the Flutter Farmer registration form while retaining submit-time validation.

Full names allow English letters, spaces, apostrophes, and hyphens only. Phone numbers allow exactly ten local digits such as `0771234567`; formatted and `+94` values are invalid at registration.

Input formatters block invalid typed, pasted, and autofilled characters. Shared submit-time validators prevent programmatic values from bypassing those filters. The layout, API call, password rules, and registration workflow remain unchanged.

Unit and widget tests cover valid values plus typed, pasted, and autofill-equivalent invalid values. Flutter formatting, analysis, and tests verify the change.
