﻿// Visual Studio Shared Project
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.Project;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ProjectUITests {
    public class ShowAllFiles {
        public void ShowAllFilesToggle(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFiles",
                    projectType,
                    ProjectGenerator.Folder("SubFolder"),
                    ProjectGenerator.Compile("SubFolder\\server"),
                    ProjectGenerator.Property("ProjectView", "ProjectFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFiles", "SubFolder", "server" + projectType.CodeExtension);
                    AutomationWrapper.Select(projectNode);

                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all

                    Assert.IsTrue(solution.GetProject("ShowAllFiles").GetIsFolderExpanded("SubFolder"));
                }
            }
        }

        public void ShowAllFilesFilesAlwaysHidden(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = MakeBasicProject(projectType);
                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFiles");
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "ShowAllFiles.sln"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "ShowAllFiles.suo"));
                }
            }
        }

        public void ShowAllFilesSymLinks(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesSymLink",
                    projectType,
                    ProjectGenerator.Folder("SubFolder")
                );
                var userDef = new ProjectDefinition(
                    def.Name,
                    projectType,
                    true,
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                );

                var solutionFile = SolutionFile.Generate(def.Name, def, userDef);

                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start("cmd.exe",
                    String.Format("/c mklink /J \"{0}\" \"{1}\"",
                        Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SymFolder"),
                        Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SubFolder")
                    ))) {
                    p.WaitForExit();
                }

                using (System.Diagnostics.Process p = System.Diagnostics.Process.Start("cmd.exe",
                    String.Format("/c mklink /J \"{0}\" \"{1}\"",
                        Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SubFolder\Infinite"),
                        Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SubFolder")
                    ))) {
                    p.WaitForExit();
                }

                try {
                    using (var solution = solutionFile.ToVs(app)) {
                        Assert.IsNotNull(solution.WaitForItem("ShowAllFilesSymLink", "SymFolder"));

                        // https://pytools.codeplex.com/workitem/1150 - infinite links, not displayed
                        Assert.IsNull(solution.FindItem("ShowAllFilesSymLink", "SubFolder", "Infinite"));

                        File.WriteAllText(
                            Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SubFolder\Foo.txt"),
                            "Hi!"
                        );

                        // https://pytools.codeplex.com/workitem/1152 - watching the sym link folder
                        Assert.IsNotNull(solution.WaitForItem("ShowAllFilesSymLink", "SubFolder", "Foo.txt"));
                        Assert.IsNotNull(solution.WaitForItem("ShowAllFilesSymLink", "SymFolder", "Foo.txt"));
                    }
                } finally {
                    using (System.Diagnostics.Process p = System.Diagnostics.Process.Start("cmd.exe",
                        String.Format("/c rmdir \"{0}\" \"{1}\"",
                            Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SymFolder"),
                            Path.Combine(solutionFile.Directory, @"ShowAllFilesSymLink\SubFolder\Infinite")
                        ))) {
                        p.WaitForExit();
                    }
                }
            }
        }

        public void ShowAllFilesLinked(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesLinked",
                    projectType,
                    ProjectGenerator.Compile("..\\File"),
                    ProjectGenerator.Folder("SubFolder"),
                    ProjectGenerator.Compile("..\\LinkedFile").Link("SubFolder\\LinkedFile"),
                    ProjectGenerator.Property("ProjectView", "ProjectFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var linkedNode = solution.WaitForItem("ShowAllFilesLinked", "File" + projectType.CodeExtension);
                    AutomationWrapper.Select(linkedNode);
                    Keyboard.ControlC();

                    var subFolder = solution.WaitForItem("ShowAllFilesLinked", "SubFolder");
                    AutomationWrapper.Select(subFolder);

                    Keyboard.ControlV();
                    solution.CheckMessageBox("Cannot copy linked files within the same project. You cannot have more than one link to the same file in a project.");

                    linkedNode = solution.WaitForItem("ShowAllFilesLinked", "SubFolder", "LinkedFile" + projectType.CodeExtension);
                    AutomationWrapper.Select(linkedNode);

                    Keyboard.ControlX();

                    var projectNode = solution.WaitForItem("ShowAllFilesLinked");
                    AutomationWrapper.Select(projectNode);

                    Keyboard.ControlV();
                    solution.GetProject("ShowAllFilesLinked").Save();

                    var text = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesLinked\ShowAllFilesLinked" + projectType.ProjectExtension));
                    Assert.IsTrue(text.IndexOf("<Link>LinkedFile" + projectType.CodeExtension + "</Link>") != -1);
                }
            }
        }

        public void ShowAllFilesIncludeExclude(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesIncludeExclude",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("ExcludeFolder1"),
                        ProjectGenerator.Folder("ExcludeFolder2"),
                        ProjectGenerator.Folder("IncludeFolder1", isExcluded: true),
                        ProjectGenerator.Folder("IncludeFolder2", isExcluded: true),
                        ProjectGenerator.Folder("IncludeFolder3", isExcluded: true),
                        ProjectGenerator.Folder("NotOnDiskFolder", isMissing: true)
                    ),
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("NotInProject", isExcluded: true),
                        ProjectGenerator.Compile("server"),
                        ProjectGenerator.Compile("NotOnDisk", isMissing: true),
                        ProjectGenerator.Content("ExcludeFolder1\\Item.txt", ""),
                        ProjectGenerator.Content("ExcludeFolder2\\Item.txt", ""),
                        ProjectGenerator.Content("IncludeFolder1\\Item.txt", "", isExcluded: true),
                        ProjectGenerator.Content("IncludeFolder2\\Item.txt", "", isExcluded: true),
                        ProjectGenerator.Content("IncludeFolder2\\Item2.txt", "", isExcluded: true),
                        ProjectGenerator.Content("IncludeFolder3\\Text.txt", "", isExcluded: true),
                        ProjectGenerator.Compile("..\\LinkedFile")
                    ),
                    ProjectGenerator.PropertyGroup(
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles"),
                        ProjectGenerator.StartupFile("server")
                    )
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesIncludeExclude");

                    var excludedFolder = solution.GetProject("ShowAllFilesIncludeExclude").ProjectItems.Item("ExcludeFolder1");
                    var itemTxt = excludedFolder.ProjectItems.Item("Item.txt");
                    var buildAction = itemTxt.Properties.Item("BuildAction");
                    Assert.IsNotNull(buildAction);

                    var notInProject = solution.WaitForItem("ShowAllFilesIncludeExclude", "NotInProject" + projectType.CodeExtension);
                    AutomationWrapper.Select(notInProject);

                    var folder = solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder1");
                    AutomationWrapper.Select(folder);
                    solution.ExecuteCommand("Project.ExcludeFromProject");

                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "ExcludeFolder1"));

                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all again
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder1"));
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder1", "Item.txt"));

                    // https://nodejstools.codeplex.com/workitem/250
                    var linkedFile = solution.WaitForItem("ShowAllFilesIncludeExclude", "LinkedFile" + projectType.CodeExtension);
                    AutomationWrapper.Select(linkedFile);
                    solution.ExecuteCommand("Project.ExcludeFromProject");
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "LinkedFile" + projectType.CodeExtension));

                    // https://pytools.codeplex.com/workitem/1153
                    // Shouldn't have a BuildAction property on excluded items
                    try {
                        solution.GetProject("ShowAllFilesIncludeExclude").ProjectItems.Item("ExcludedFolder1").ProjectItems.Item("Item.txt").Properties.Item("BuildAction");
                        Assert.Fail("Excluded item had BuildAction");
                    } catch (ArgumentException) {
                    }

                    var file = solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder2", "Item.txt");
                    AutomationWrapper.Select(file);
                    solution.ExecuteCommand("Project.ExcludeFromProject");

                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "ExcludeFolder2", "Item.txt"));
                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all

                    var itemTxtNode = solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder2", "Item.txt");
                    Assert.IsNotNull(itemTxtNode);

                    AutomationWrapper.Select(itemTxtNode);

                    // https://pytools.codeplex.com/workitem/1143
                    try {
                        solution.ExecuteCommand("Project.AddNewItem");
                        Assert.Fail("Added a new item on excluded node");
                    } catch (COMException) {
                    }

                    var excludedFolderNode = solution.WaitForItem("ShowAllFilesIncludeExclude", "ExcludeFolder1");
                    Assert.IsNotNull(excludedFolderNode);
                    AutomationWrapper.Select(excludedFolderNode);
                    try {
                        solution.ExecuteCommand("Project.NewFolder");
                        Assert.Fail("Added a new folder on excluded node");
                    } catch (COMException) {
                    }

                    // include
                    folder = solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder1");
                    AutomationWrapper.Select(folder);
                    solution.ExecuteCommand("Project.IncludeInProject");
                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder1"));
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder1", "Item.txt"));

                    // https://nodejstools.codeplex.com/workitem/242
                    // Rename Item.txt on disk
                    File.Move(
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFilesIncludeExclude\IncludeFolder1\Item.txt"),
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFilesIncludeExclude\IncludeFolder1\ItemNew.txt")
                    );

                    var includedItem = solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder1", "Item.txt");
                    AutomationWrapper.Select(includedItem);
                    solution.ExecuteCommand("Project.ExcludeFromProject");

                    // Rename it back
                    File.Move(
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFilesIncludeExclude\IncludeFolder1\ItemNew.txt"),
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFilesIncludeExclude\IncludeFolder1\Item.txt")
                    );

                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all

                    // item should be back
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder1", "Item.txt"));

                    folder = solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder2", "Item.txt");
                    AutomationWrapper.Select(folder);
                    solution.ExecuteCommand("Project.IncludeInProject");
                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder2"));
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder2", "Item.txt"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "IncludeFolder2", "Item2.txt"));

                    AutomationWrapper.Select(projectNode);
                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all

                    // exclude an item which exists, but is not on disk, it should be removed
                    var notOnDisk = solution.WaitForItem("ShowAllFilesIncludeExclude", "NotOnDisk" + projectType.CodeExtension);
                    AutomationWrapper.Select(notOnDisk);
                    solution.ExecuteCommand("Project.ExcludeFromProject");
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "NotOnDisk" + projectType.CodeExtension));

                    var notOnDiskFolder = solution.WaitForItem("ShowAllFilesIncludeExclude", "NotOnDiskFolder");
                    AutomationWrapper.Select(notOnDiskFolder);
                    solution.ExecuteCommand("Project.ExcludeFromProject");
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "NotOnDiskFolder"));

                    // https://pytools.codeplex.com/workitem/1138
                    var server = solution.FindItem("ShowAllFilesIncludeExclude", "server" + projectType.CodeExtension);

                    AutomationWrapper.Select(server);
                    Keyboard.ControlC();
                    System.Threading.Thread.Sleep(1000);

                    var includeFolder3 = solution.FindItem("ShowAllFilesIncludeExclude", "IncludeFolder3");
                    AutomationWrapper.Select(includeFolder3);

                    Keyboard.ControlV();
                    System.Threading.Thread.Sleep(1000);

                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    // folder should now be included
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesIncludeExclude", "IncludeFolder3"));

                    // https://nodejstools.codeplex.com/workitem/250
                    // Excluding the startup item, and then including it again, it should be bold
                    server = solution.FindItem("ShowAllFilesIncludeExclude", "server" + projectType.CodeExtension);
                    AutomationWrapper.Select(server);
                    solution.ExecuteCommand("Project.ExcludeFromProject");

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesIncludeExclude", "server" + projectType.CodeExtension));
                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing all
                    server = solution.FindItem("ShowAllFilesIncludeExclude", "server" + projectType.CodeExtension);
                    AutomationWrapper.Select(server);

                    solution.ExecuteCommand("Project.IncludeInProject");
                    System.Threading.Thread.Sleep(2000);

                    Assert.IsTrue(solution.GetProject("ShowAllFilesIncludeExclude").GetIsItemBolded("server" + projectType.CodeExtension));
                }
            }
        }

        public void ShowAllFilesChanges(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = MakeBasicProject(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFiles");
                    AutomationWrapper.Select(projectNode);

                    var dteProject = solution.GetProject("ShowAllFiles");

                    solution.AssertFileExists("ShowAllFiles", "NotInProject" + projectType.CodeExtension);

                    // everything should be there...
                    solution.AssertFolderExists("ShowAllFiles", "Folder");
                    solution.AssertFileExists("ShowAllFiles", "Folder", "File" + projectType.CodeExtension);
                    solution.AssertFileExists("ShowAllFiles", "Folder", "File.txt");
                    solution.AssertFolderExists("ShowAllFiles", "Folder", "SubFolder");
                    solution.AssertFileExists("ShowAllFiles", "Folder", "SubFolder", "SubFile.txt");
                                        
                    // create some stuff, it should show up...
                    File.WriteAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFile.txt"), "");
                    solution.AssertFileExists("ShowAllFiles", "NewFile.txt");
                    
                    File.WriteAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\Folder\NewFile.txt"), "");
                    solution.AssertFileExists("ShowAllFiles", "Folder", "NewFile.txt");
                    Assert.IsTrue(dteProject.GetIsFolderExpanded(@"Folder"));

                    File.WriteAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\Folder\SubFolder\NewFile.txt"), "");
                    solution.AssertFileExists("ShowAllFiles", "Folder", "SubFolder", "NewFile.txt");

                    Directory.CreateDirectory(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder"));
                    solution.AssertFolderExists("ShowAllFiles", "NewFolder");
                    
                    Directory.CreateDirectory(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder\SubFolder"));
                    solution.AssertFolderExists("ShowAllFiles", "NewFolder", "SubFolder");
                    
                    File.WriteAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder\SubFolder\NewFile.txt"), "");
                    solution.AssertFileExists("ShowAllFiles", "NewFolder", "SubFolder", "NewFile.txt");
                    
                    // delete some stuff, it should go away
                    File.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\Folder\File.txt"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "Folder", "File.txt"));
                    
                    File.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFile.txt"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "NewFile.txt"));
                    
                    File.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder\NewFile.txt"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "NewFolder", "NewFile.txt"));
                    
                    File.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder\SubFolder\NewFile.txt"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "NewFolder", "SubFolder", "NewFile.txt"));
                    
                    Directory.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder\SubFolder"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "NewFolder", "SubFolder"));
                    
                    Directory.Delete(Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\NewFolder"));
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "NewFolder"));
                    
                    Directory.Move(
                        Path.Combine(solution.SolutionDirectory, @"MovedIntoShowAllFiles"),
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\MovedIntoShowAllFiles")
                    );

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFiles", "MovedIntoShowAllFiles", "Text.txt"));
                    
                    // move it back
                    Directory.Move(
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\MovedIntoShowAllFiles"),
                        Path.Combine(solution.SolutionDirectory, @"MovedIntoShowAllFiles")
                    );

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFiles", "MovedIntoShowAllFiles", "Text.txt"));

                    // and move it back into the project one more time
                    Directory.Move(
                        Path.Combine(solution.SolutionDirectory, @"MovedIntoShowAllFiles"),
                        Path.Combine(solution.SolutionDirectory, @"ShowAllFiles\MovedIntoShowAllFiles")
                    );

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFiles", "MovedIntoShowAllFiles", "Text.txt"));
                }
            }
        }

        public void ShowAllFilesHiddenFiles(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesHiddenFiles",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder", isExcluded: true),
                        ProjectGenerator.Folder("HiddenFolder", isExcluded: true)
                    ),
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("NotInProject", isExcluded: true),
                        ProjectGenerator.Compile("NotInProject2", isExcluded: true),
                        ProjectGenerator.Compile("server"),
                        ProjectGenerator.Content("Folder\\File.txt", "", isExcluded: true),
                        ProjectGenerator.Content("Folder\\File2.txt", "", isExcluded: true),
                        ProjectGenerator.Content("HiddenFolder\\HiddenFile.txt", "", isExcluded: true)
                    ),
                    ProjectGenerator.PropertyGroup(
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                    )
                );

                var solutionFile = def.Generate();
                var file = Path.Combine(solutionFile.Directory, @"ShowAllFilesHiddenFiles\NotInProject" + projectType.CodeExtension);
                File.SetAttributes(
                    file,
                    File.GetAttributes(file) | FileAttributes.Hidden
                );
                file = Path.Combine(solutionFile.Directory, @"ShowAllFilesHiddenFiles\Folder\File.txt");
                File.SetAttributes(
                    file,
                    File.GetAttributes(file) | FileAttributes.Hidden
                );
                file = Path.Combine(solutionFile.Directory, @"ShowAllFilesHiddenFiles\HiddenFolder");
                File.SetAttributes(
                    file,
                    File.GetAttributes(file) | FileAttributes.Hidden
                );


                using (var solution = solutionFile.ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesHiddenFiles");

                    // hidden files/folders shouldn't be visible
                    Assert.IsNull(solution.FindItem("ShowAllFilesHiddenFiles", "NotInProject" + projectType.CodeExtension));
                    Assert.IsNull(solution.FindItem("ShowAllFilesHiddenFiles", "Folder", "File.txt"));
                    Assert.IsNull(solution.FindItem("ShowAllFilesHiddenFiles", "HiddenFolder"));

                    // but if they change back, they should be
                    file = Path.Combine(solution.SolutionDirectory, @"ShowAllFilesHiddenFiles\NotInProject" + projectType.CodeExtension);
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) & ~FileAttributes.Hidden
                    );

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesHiddenFiles", "NotInProject" + projectType.CodeExtension));
                    file = Path.Combine(solution.SolutionDirectory, @"ShowAllFilesHiddenFiles\Folder\File.txt");
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) & ~FileAttributes.Hidden
                    );

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesHiddenFiles", "Folder", "File.txt"));
                    file = Path.Combine(solution.SolutionDirectory, @"ShowAllFilesHiddenFiles\HiddenFolder");
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) & ~FileAttributes.Hidden
                    );
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesHiddenFiles", "HiddenFolder"));

                    // changing non-hidden items to hidden should cause them to be removed
                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesHiddenFiles", "NotInProject2" + projectType.CodeExtension));
                    file = Path.Combine(solution.SolutionDirectory, @"ShowAllFilesHiddenFiles\NotInProject2" + projectType.CodeExtension);
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) | FileAttributes.Hidden
                    );
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesHiddenFiles", "NotInProject2" + projectType.CodeExtension));

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesHiddenFiles", "Folder"));
                    file = Path.Combine(solution.SolutionDirectory, @"ShowAllFilesHiddenFiles\Folder");
                    File.SetAttributes(
                        file,
                        File.GetAttributes(file) | FileAttributes.Hidden
                    );
                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesHiddenFiles", "Folder"));
                }
            }

        }

        public void ShowAllFilesOnPerUser(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var solutionFile = SolutionFile.Generate("ShowAllFilesOnPerUser",
                    new ProjectDefinition(
                        "ShowAllFilesOnPerUser",
                        projectType,
                        ProjectGenerator.Compile("NotInProject", isExcluded: true)
                    ),
                    new ProjectDefinition(
                        "ShowAllFilesOnPerUser",
                        projectType,
                    /* isUserProject: */ true,
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                    )
                );

                using (var solution = solutionFile.ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesOnPerUser");
                    AutomationWrapper.Select(projectNode);

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesOnPerUser", "NotInProject" + projectType.CodeExtension));

                    // change setting, UI should be updated
                    solution.ExecuteCommand("Project.ShowAllFiles");

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesOnPerUser", "NotInProject" + projectType.CodeExtension));

                    // save setting, user project file should be updated
                    solution.GetProject("ShowAllFilesOnPerUser").Save();

                    var projectText = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesOnPerUser\ShowAllFilesOnPerUser" + projectType.ProjectExtension + ".user"));
                    Assert.IsTrue(projectText.Contains("<ProjectView>ProjectFiles</ProjectView>"));
                }
            }
        }

        public void ShowAllFilesOnPerProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesOnPerProject",
                    projectType,
                    ProjectGenerator.Compile("NotInProject", isExcluded: true),
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesOnPerProject");
                    AutomationWrapper.Select(projectNode);

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesOnPerProject", "NotInProject" + projectType.CodeExtension));

                    // change setting, UI should be updated
                    solution.ExecuteCommand("Project.ShowAllFiles");

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesOnPerProject", "NotInProject" + projectType.CodeExtension));

                    // save setting, project file should be updated
                    solution.GetProject("ShowAllFilesOnPerProject").Save();

                    var projectText = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesOnPerProject\ShowAllFilesOnPerProject" + projectType.ProjectExtension));
                    Assert.IsTrue(projectText.Contains("<ProjectView>ProjectFiles</ProjectView>"));
                }
            }

        }

        public void ShowAllFilesOffPerUser(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var solutionFile = SolutionFile.Generate("ShowAllFilesOffPerUser",
                    new ProjectDefinition(
                        "ShowAllFilesOffPerUser",
                        projectType,
                        ProjectGenerator.Compile("NotInProject", isExcluded: true)
                    ),
                    new ProjectDefinition(
                        "ShowAllFilesOffPerUser",
                        projectType,
                    /* isUserProject: */ true,
                        ProjectGenerator.Property("ProjectView", "ProjectFiles")
                    )
                );

                using (var solution = solutionFile.ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesOffPerUser");
                    AutomationWrapper.Select(projectNode);

                    Assert.IsNull(solution.FindItem("ShowAllFilesOffPerUser", "NotInProject" + projectType.CodeExtension));

                    // change setting, UI should be updated
                    solution.ExecuteCommand("Project.ShowAllFiles");

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesOffPerUser", "NotInProject" + projectType.CodeExtension));

                    // save setting, user project file should be updated
                    solution.GetProject("ShowAllFilesOffPerUser").Save();

                    var projectText = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesOffPerUser\ShowAllFilesOffPerUser" + projectType.ProjectExtension + ".user"));
                    Assert.IsTrue(projectText.Contains("<ProjectView>ShowAllFiles</ProjectView>"));
                }
            }
        }

        public void ShowAllFilesOffPerProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesOffPerProject",
                    projectType,
                    ProjectGenerator.Compile("NotInProject", isExcluded: true),
                    ProjectGenerator.Property("ProjectView", "ProjectFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesOffPerProject");
                    AutomationWrapper.Select(projectNode);

                    Assert.IsNull(solution.FindItem("ShowAllFilesOffPerProject", "NotInProject" + projectType.CodeExtension));

                    // change setting, UI should be updated
                    solution.ExecuteCommand("Project.ShowAllFiles");

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesOffPerProject", "NotInProject" + projectType.CodeExtension));

                    // save setting, project file should be updated
                    solution.GetProject("ShowAllFilesOffPerProject").Save();

                    var projectText = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesOffPerProject\ShowAllFilesOffPerProject" + projectType.ProjectExtension));
                    Assert.IsTrue(projectText.Contains("<ProjectView>ShowAllFiles</ProjectView>"));
                }
            }
        }

        public void ShowAllFilesDefault(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesDefault",
                    projectType,
                    ProjectGenerator.Folder("SubFolder"),
                    ProjectGenerator.Compile("SubFolder\\server"),
                    ProjectGenerator.Compile("NotInProject", isExcluded: true),
                    ProjectGenerator.Property("ProjectView", "")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesDefault");
                    AutomationWrapper.Select(projectNode);


                    Assert.IsNull(solution.FindItem("ShowAllFilesDefault", "NotInProject" + projectType.CodeExtension));

                    // change setting, UI should be updated
                    solution.ExecuteCommand("Project.ShowAllFiles");

                    Assert.IsNotNull(solution.WaitForItem("ShowAllFilesDefault", "NotInProject" + projectType.CodeExtension));

                    // save setting, user project file should be updated
                    solution.GetProject("ShowAllFilesDefault").Save();

                    var projectText = File.ReadAllText(Path.Combine(solution.SolutionDirectory, @"ShowAllFilesDefault\ShowAllFilesDefault" + projectType.ProjectExtension + ".user"));
                    Assert.IsTrue(projectText.Contains("<ProjectView>ShowAllFiles</ProjectView>"));
                }
            }
        }

        /// <summary>
        /// https://nodejstools.codeplex.com/workitem/240
        /// </summary>
        public void ShowAllMoveNotInProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllMoveNotInProject",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder", isExcluded: true),
                        ProjectGenerator.Folder("Folder\\SubFolder", isExcluded: true),
                        ProjectGenerator.Compile("NotInProject", isExcluded:true),
                        ProjectGenerator.Compile("Folder\\File", isExcluded: true),
                        ProjectGenerator.Content("Folder\\SubFolder\\SubFile.txt", "", isExcluded: true)
                    ),
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    solution.WaitForItem("ShowAllMoveNotInProject");

                    var file = solution.WaitForItem("ShowAllMoveNotInProject", "NotInProject" + projectType.CodeExtension);
                    AutomationWrapper.Select(file);
                    Keyboard.ControlX();
                    System.Threading.Thread.Sleep(1000);

                    var folder = solution.WaitForItem("ShowAllMoveNotInProject", "Folder");
                    AutomationWrapper.Select(folder);
                    Keyboard.ControlV();

                    Assert.IsNotNull(solution.WaitForItem("ShowAllMoveNotInProject", "Folder", "NotInProject" + projectType.CodeExtension));

                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllMoveNotInProject", "Folder", "NotInProject" + projectType.CodeExtension));

                    solution.ExecuteCommand("Project.ShowAllFiles"); // start showing again

                    var subFolder = solution.WaitForItem("ShowAllMoveNotInProject", "Folder", "SubFolder");
                    AutomationWrapper.Select(subFolder);

                    Keyboard.ControlX();
                    System.Threading.Thread.Sleep(1000);
                    var projectNode = solution.WaitForItem("ShowAllMoveNotInProject");
                    AutomationWrapper.Select(projectNode);
                    Keyboard.ControlV();
                    //app.MaybeCheckMessageBox(MessageBoxButton.Yes, "A file named");
                    Assert.IsNotNull(solution.WaitForItem("ShowAllMoveNotInProject", "SubFolder"));

                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNull(solution.WaitForItemRemoved("ShowAllMoveNotInProject", "SubFolder"));
                }
            }
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1996
        /// </summary>
        public void ShowAllExcludeSelected(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllExcludeSelected",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder"),
                        ProjectGenerator.Compile("Folder\\File1"),
                        ProjectGenerator.Compile("Folder\\File2")
                    ),
                    ProjectGenerator.Property("ProjectView", "ProjectFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    solution.WaitForItem("ShowAllExcludeSelected");

                    var file = solution.WaitForItem("ShowAllExcludeSelected", "Folder", "File2" + projectType.CodeExtension);
                    AutomationWrapper.Select(file);
                    solution.ExecuteCommand("Project.ExcludeFromProject");

                    solution.WaitForItemRemoved("ShowAllExcludeSelected", "Folder", "File2" + projectType.CodeExtension);

                    Assert.AreEqual("File1" + projectType.CodeExtension, Path.GetFileName(GetSelectedItemName(app)));

                    file = solution.WaitForItem("ShowAllExcludeSelected", "Folder", "File1" + projectType.CodeExtension);
                    AutomationWrapper.Select(file);
                    solution.ExecuteCommand("Project.ExcludeFromProject");
                    solution.WaitForItemRemoved("ShowAllExcludeSelected", "Folder", "File1" + projectType.CodeExtension);

                    Assert.AreEqual("Folder", Path.GetFileName(GetSelectedItemName(app).TrimEnd('\\')));
                }
            }
        }

        private static string GetSelectedItemName(VisualStudioApp app) {
            var window = UIHierarchyUtilities.GetUIHierarchyWindow(
                app.ServiceProvider,
                new Guid(ToolWindowGuids80.SolutionExplorer)
            );
            IntPtr hier;
            uint itemid;
            IVsMultiItemSelect select;
            ErrorHandler.ThrowOnFailure(window.GetCurrentSelection(out hier, out itemid, out select));
            Assert.AreNotEqual(IntPtr.Zero, hier);
            Assert.IsNull(select);
            try {
                var obj = (IVsHierarchy)Marshal.GetObjectForIUnknown(hier);
                string name;
                ErrorHandler.ThrowOnFailure(obj.GetCanonicalName(itemid, out name));
                return name;
            } finally {
                Marshal.Release(hier);
            }
        }


        /// <summary>
        /// Creating & deleting files rapidly shouldn't leave a file behind
        /// 
        /// https://nodejstools.codeplex.com/workitem/380
        /// </summary>
        public void ShowAllFilesRapidChanges(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesRapidChanges",
                    projectType,
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesRapidChanges");
                    AutomationWrapper.Select(projectNode);

                    List<string> addedItems = new List<string>();
                    for (int i = 0; i < 1000; i++) {
                        var filename = Path.Combine(solution.SolutionDirectory, "ShowAllFilesRapidChanges", Path.GetRandomFileName());
                        File.WriteAllText(filename, "");
                        File.Delete(filename);
                        addedItems.Add(filename);
                    }

                    foreach (var item in addedItems) {
                        Assert.IsNull(solution.WaitForItemRemoved("ShowAllFilesRapidChanges", Path.GetFileName(item)));
                    }
                }
            }
        }

        /// <summary>
        /// Creating & deleting and then re-creating files rapidly should have the files be 
        /// present in solution explorer.
        /// </summary>
        public void ShowAllFilesRapidChanges2(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesRapidChanges",
                    projectType,
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesRapidChanges");
                    AutomationWrapper.Select(projectNode);

                    HashSet<string> addedItems = new HashSet<string>();
                    for (int i = 0; i < 100; i++) {
                        var filename = Path.Combine(solution.SolutionDirectory, "ShowAllFilesRapidChanges", Path.GetRandomFileName());
                        File.WriteAllText(filename, "");
                        File.Delete(filename);
                        File.WriteAllText(filename, "");
                        addedItems.Add(filename);
                    }

                    // give some time for changes to be processed...
                    System.Threading.Thread.Sleep(10000);

                    foreach (var item in addedItems) {
                        Assert.IsNotNull(solution.WaitForItem("ShowAllFilesRapidChanges", Path.GetFileName(item)), item);
                    }
                }
            }
        }

        public void ShowAllFilesCopyExcludedFolderWithItemByKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            ShowAllFilesCopyExcludedFolderWithItem(app, pg, DragDropCopyCutPaste.CopyByKeyboard);
        }

        public void ShowAllFilesCopyExcludedFolderWithItemByMouse(VisualStudioApp app, ProjectGenerator pg) {
            ShowAllFilesCopyExcludedFolderWithItem(app, pg, DragDropCopyCutPaste.CopyByMouse);
        }

        /// <summary>
        /// https://nodejstools.codeplex.com/workitem/475
        /// </summary>
        private void ShowAllFilesCopyExcludedFolderWithItem(VisualStudioApp app, ProjectGenerator pg, DragDropCopyCutPaste.MoveDelegate copier) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "CopyExcludedFolderWithItem",
                    projectType,
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles"),
                    ProjectGenerator.Folder("NewFolder1"),
                    ProjectGenerator.Folder("NewFolder2", isExcluded: true),
                    ProjectGenerator.Compile("server", isExcluded: true)
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("CopyExcludedFolderWithItem");
                    AutomationWrapper.Select(projectNode);

                    copier(
                        solution,
                        solution.WaitForItem("CopyExcludedFolderWithItem", "NewFolder1"),
                        solution.WaitForItem("CopyExcludedFolderWithItem", "NewFolder2")
                    );

                    Assert.IsNotNull(
                        solution.WaitForItem("CopyExcludedFolderWithItem", "NewFolder1", "NewFolder2")
                    );
                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all
                    
                    Assert.IsNull(
                        solution.WaitForItemRemoved("CopyExcludedFolderWithItem", "NewFolder1", "NewFolder2")
                    );
                }
            }
        }

        public void ShowAllFilesMoveExcludedItemToExcludedFolderByKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            ShowAllFilesMoveExcludedItemToExcludedFolder(app, pg, DragDropCopyCutPaste.MoveByKeyboard);
        }

        public void ShowAllFilesMoveExcludedItemToExcludedFolderByMouse(VisualStudioApp app, ProjectGenerator pg) {
            ShowAllFilesMoveExcludedItemToExcludedFolder(app, pg, DragDropCopyCutPaste.MoveByMouse);
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1909
        /// </summary>
        private void ShowAllFilesMoveExcludedItemToExcludedFolder(VisualStudioApp app, ProjectGenerator pg, DragDropCopyCutPaste.MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var def = new ProjectDefinition(
                    "ShowAllFilesMoveExcludedItemToExcludedFolder",
                    projectType,
                    ProjectGenerator.Property("ProjectView", "ShowAllFiles"),
                    ProjectGenerator.Folder("NewFolder1", isExcluded: true),
                    ProjectGenerator.Compile("server", isExcluded: true)
                );

                using (var solution = def.Generate().ToVs(app)) {
                    var projectNode = solution.WaitForItem("ShowAllFilesMoveExcludedItemToExcludedFolder");
                    AutomationWrapper.Select(projectNode);

                    mover(
                        solution,
                        solution.WaitForItem("ShowAllFilesMoveExcludedItemToExcludedFolder", "NewFolder1"),
                        solution.WaitForItem("ShowAllFilesMoveExcludedItemToExcludedFolder", "server" + projectType.CodeExtension)
                    );

                    Assert.IsNotNull(
                        solution.WaitForItem("ShowAllFilesMoveExcludedItemToExcludedFolder", "NewFolder1", "server" + projectType.CodeExtension)
                    );
                    solution.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

                    Assert.IsNull(
                        solution.WaitForItemRemoved("ShowAllFilesMoveExcludedItemToExcludedFolder", "NewFolder1")
                    );
                    Assert.IsNull(
                        solution.WaitForItemRemoved("ShowAllFilesMoveExcludedItemToExcludedFolder", "NewFolder1", "server" + projectType.CodeExtension)
                    );
                }
            }
        }

        private static ProjectDefinition MakeBasicProject(ProjectType projectType) {
            var def = new ProjectDefinition(
                "ShowAllFiles",
                projectType,
                ProjectGenerator.ItemGroup(
                    ProjectGenerator.Folder("Folder", isExcluded: true),
                    ProjectGenerator.Folder("Folder\\SubFolder", isExcluded: true),
                    ProjectGenerator.Content("Folder\\SubFolder\\SubFile.txt", "", isExcluded: true),
                    ProjectGenerator.Content("Folder\\SubFolder\\File.txt", "", isExcluded: true),
                    ProjectGenerator.Compile("NotInProject", isExcluded: true),
                    ProjectGenerator.Compile("Folder\\File", isExcluded: true),
                    ProjectGenerator.Content("Folder\\File.txt", "", isExcluded: true),
                    ProjectGenerator.Compile("server"),
                    ProjectGenerator.Content("ShowAllFiles.v11.suo", "", isExcluded: true),
                    ProjectGenerator.Folder("..\\MovedIntoShowAllFiles", isExcluded: true),
                    ProjectGenerator.Content("..\\MovedIntoShowAllFiles\\Text.txt", "", isExcluded: true)
                ),
                ProjectGenerator.Property("ProjectView", "ShowAllFiles")
            );

            return def;
        }
    }
}