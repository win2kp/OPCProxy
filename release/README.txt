1. 要求，OPCService服务端必须和KEPWARE装在同一台机器上（配置文件里面HOST=127.0.0.1不能改)
2. 记得在防火墙上给服务器开端口，sample.xml里面写的是TCP 9100
3. OPCService.exe 不带参数直接运行会列出所有参数用法，然后退出。所以直接双击是不行的
4. OPCService.exe 用批处理执行起来以后是个可以输入命令的命令行环境，可用命令输help按回车
5. 需要在kepware里面先配置好Channel和Device还有Tags先

目录结构

~/OPCService/OPCService.exe   OPC服务主程序（直接执行闪退，需要用批处理加参数执行）
~/OPCService/start_sample.cmd   启动服务器用的批处理（需要自己改一下里面加载的配置xml文件)
~/OPCService/sample.xml      示例配置文件（记事本打开里面有注释)
~/OPCService/GUIuncTest	      服务器测试工具，可以远端测试服务器是否正常
~/OPCServiceClient	OPC服务客户端dll(添加引用，剩下的看例子代码）
~/sample		客户端调用的例子代码
~/OPCService/OPCProfileEdit.exe   用来编辑配置文件的工具

