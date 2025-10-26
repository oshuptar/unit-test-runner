### Overview

MiniTestRunner is a custom-built unit test runner developed as part of the Programming 3 — Advanced course project.
The goal of this project was to deepen understanding of Reflection in C# by implementing a simplified framework that can automatically discover and execute unit tests from a given compiled assembly (DLL).

### How to run:
In order to run the program one has to specify the `full path` as the input parameter to the program: `./MiniTestRunner <full_path_to_test_assembly>`. <br>
Example prompt looks as following:
`./MiniTestRunner ~/c\#-projects/unit-test-runner/MiniTest/AuthenticationService.Tests/bin/Debug/net8.0/AuthenticationService.Tests.dll`. 
In the example output, you should see exactly which tests were passed and their summary.

The program loads the assembly file and starts scaning for attributes.

