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

  stream.write(`import { createRpcInvoker } from "cooke-rpc";\n\n`);

  // Register all types that participate in a union since then they need the $type discriminator
  const requiresDiscriminator = new Set();
  for (const type of meta.types) {
    if (type.category === "union") {
      for (const memberType of type.types) {
        requiresDiscriminator.add(memberType.name);
      }
    } else if (type.category === "complex") {
      if (type.extenders && type.extenders.length > 0) {
        requiresDiscriminator.add(type.name);
        for (const ext of type.extenders) {
          requiresDiscriminator.add(ext.name);
        }
      }
    }
  }

  for (const type of meta.types) {
    if (type.category === "union") {
      stream.write(`export type ${type.name} = `);
      if (type.types.length > 0) {
        stream.write(type.types.map(formatType).join(" | "));
      } else {
        stream.write("never");
      }
      stream.write(";\n\n");
    } else if (type.category === "complex") {
      stream.write(`export type ${type.name} = `);
      stream.write("{\n");
      if (requiresDiscriminator.has(type.name)) {
        stream.write(`  $type: "${type.name}";\n`);
      } else {
        stream.write(`  $type?: "${type.name}";\n`);
      }

      stream.write(
        type.properties
          .map(
            (p) =>
              `  ${p.name}${
                (p.type.category === "generic" && p.type.name === "optional") ||
                p.optional
                  ? "?"
                  : ""
              }: ${formatType(p.type)};`
          )
          .join("\n")
      );

      stream.write("\n}");

      if (type.extenders) {
        stream.write(" | ");
        stream.write(type.extenders.map(formatType).join(" | "));
      }

      stream.write(";\n\n");
    } else if (type.category === "enum") {
      stream.write(`export enum ${type.name} {\n`);
      stream.write(
        type.members
          .map(
            (x) =>
              `  ${x.name.match(/[^\w]/) ? `"${x.name}"` : x.name} = "${
                x.name
              }"`
          )
          .join(",\n")
      );
      stream.write("\n}\n\n");
    } else if (type.category === "scalar") {
      stream.write(
        `export type ${type.name} = ${formatType(type.implementationType)};\n\n`
      );
    }
  }

  for (const service of meta.services) {
    stream.write(`export const ${toCamelCase(service.name)} = {\n`);

    for (const proc of service.procedures) {
      const argsType = `[${proc.parameters
        .map((p) => `${p.name}: ${formatType(p.type)}`)
        .join(", ")}]`;
      stream.write(
        `  ${toCamelCase(proc.name)}: createRpcInvoker<'${service.name}', '${
          proc.name
        }', ${argsType}, ${formatType(proc.returnType)}>('${service.name}', '${
          proc.name
        }'),\n`
      );
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
  switch (type.category) {
    case "union":
      return type.types.map(formatType).join(" | ");

    case "native":
      return type.name;

    case "generic":
      switch (type.name) {
        case "array":
          return `Array<${type.typeArguments.map(formatType).join(",")}>`;

        case "tuple":
          return `[${type.typeArguments.map(formatType).join(",")}]`;

        case "optional":
          return `${type.typeArguments.map(formatType).join(",")} | undefined`;

        case "map": {
          const keyType = formatType(type.typeArguments[0]);
          const valueType = formatType(type.typeArguments[1]);
          if (keyType === "string" || keyType === "number") {
            return `{[key: ${keyType}]: ${valueType}}`;
          } else {
            return `{[key in ${keyType}]?: ${valueType}}`;
          }
        }

        default:
          return `${type.name}<${type.typeArguments
            .map(formatType)
            .join(",")}>`;
      }

    default:
      return type.name;
  }
}
