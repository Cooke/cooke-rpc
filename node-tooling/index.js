#!/usr/bin/env node

const https = require("https");
const http = require("http");
const fs = require("fs");
const path = require("path");

const [command, rpcUrl, outputFilePath] = [
  process.argv[2],
  process.argv[3],
  process.argv[4],
];

function printSyntax() {
  console.info("Syntax: cooke-rpc generate <http endpoint> <output file>");
}

if (command !== "generate") {
  console.error("Unknown command.");
  printSyntax();
  return;
}

if (!rpcUrl || !outputFilePath) {
  console.error("Incorrect input paramters.");
  printSyntax();
  return;
}

const outputDir = path.dirname(outputFilePath);
if (!fs.existsSync(outputDir)) {
  fs.mkdirSync(outputDir);
}

(rpcUrl.startsWith("https") ? https : http)
  .request(rpcUrl + "/introspection", (res) => {
    let data = "";
    res.on("data", (d) => (data += d));

    res.on("end", () => {
      const meta = JSON.parse(data);
      generateRpcTs(meta);
    });
  })
  .on("error", console.error)
  .end();

function generateRpcTs(meta) {
  const stream = fs.createWriteStream(outputFilePath, {
    flags: "w",
  });

  stream.write("// tslint:disable\n");

  stream.write(`import type { RpcInvocation } from "cooke-rpc";\n\n`);

  // Register all types that participate in a union since then they need the $type descriminator
  const inUnion = new Set();
  for (const type of meta.types) {
    if (type.type === "union") {
      for (const memberType of type.types) {
        inUnion.add(memberType.name);
      }
    }
  }

  for (const type of meta.types) {
    if (type.type === "union") {
      stream.write(`export type ${type.name} = `);
      stream.write(type.types.map(formatType).join(" | "));
      stream.write(";\n\n");
    } else if (type.type === "type") {
      stream.write(`export type ${type.name} = `);
      stream.write("{\n");
      if (
        inUnion.has(type.name) ||
        (type.extenders && type.extenders.length > 0)
      ) {
        stream.write(`  $type: "${type.name}";\n`);
      }

      stream.write(
        type.properties
          .map((p) => `  ${p.name}: ${formatType(p.type)};`)
          .join("\n")
      );

      stream.write("\n}");

      if (type.extenders) {
        stream.write(type.extenders.map(formatType).join(" | "));
      }

      stream.write(";\n\n");
    } else if (type.type === "enum") {
      stream.write(`export enum ${type.name} {\n`);
      stream.write(
        type.members.map((x) => `  ${x.name} = "${x.name}"`).join(",\n")
      );
      stream.write("\n}\n\n");
    }
  }

  for (const service of meta.services) {
    stream.write(`export const ${toCamelCase(service.name)} = {\n`);

    for (const proc of service.procedures) {
      stream.write(
        `  ${toCamelCase(proc.name)}: function (...args: [${proc.parameters
          .map((p) => `${p.name}: ${formatType(p.type)}`)
          .join(", ")}]): RpcInvocation<${formatType(proc.returnType)}> {\n`
      );

      stream.write(
        `    return { service: '${service.name}', proc: '${proc.name}', args: args };\n`
      );

      stream.write("  },\n");
    }

    stream.write(`}\n\n`);
  }

  stream.end();
}

function toCamelCase(str) {
  if (str[0] !== str[0].toLowerCase()) {
    return str[0].toLowerCase() + str.substr(1);
  }

  return str;
}

function formatType(type) {
  if (type.name === "array") {
    return `Array<${type.args.map(formatType).join(",")}>`;
  }

  if (type.args) {
    return `${type.name}<${type.args.map(formatType).join(",")}>`;
  }

  return type.name;
}
