# MaaEnd-Webhook-Retransmitter
MaaEnd的Webhook任务默认使用HTTP GET实现, 但大多数平台的Webhook依赖HTTP POST来接受数据, 因此多数情况下它不能有效的进行消息推送.

使用本程序, 通过本地服务转发MaaEnd的Webhook操作为HTTP POST, 通过自定义body实现多平台消息通知.



## 使用方法
1. 从Release处下载程序, 无需安装其它环境.
2. 启动前, 需在程序的exe文件所在目录创建两个配置文件. 直接新建记事本, 编辑内容然后改名即可.

- 第一个: `appsettings.Local.json`
  - ServerUrl: 本地服务使用的Url, 通常不用改.
  - ForwardUrl: 你推送的目标Url, 可能是某个平台的机器人Url, 或者平台为你提供的推送Url. 此处不需要携带参数.
  - ContentType: 不需要改.


```
{
    "App": {
        "ServerUrl": "http://localhost:17464/",
        "ForwardUrl": "你推送的目标Url",
        "ContentType": "application/json"
    }
}
```

- 第二个: `body.json`

  根据你推送目标的格式来确定这里怎么写, 查看对应平台的文档即可. 我这里以企业微信做示例.

  特别注意, 程序支持使用HTTP的**查询参数**动态传递信息, 你可以在消息体部分使用`RUNTIME_MSG`这段字符, 程序运行时会将其替换为你从Url提供的实际文本.

```
{
    "msgtype": "text",
    "text": {
        "content": "RUNTIME_MSG"
    }
}
```

3. 双击exe运行服务.
4. 在MaaEnd主程序里添加webhook操作, 将url设置为配置文件里的`ServerUrl`即可使用消息转发功能.
   - 可以使用HTTP查询参数`?msg=`来传递消息.  如填写`http://localhost:17464/?msg=测试`将会将`body.json`里的`RUNTIME_MSG`替换为`"测试"`进行发送.
5. 在多任务之间穿插步骤4, 即可实现手动消息推送功能.